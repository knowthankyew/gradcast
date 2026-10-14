using System.Diagnostics;
using Dapper;
using GradCast.Api.Data;
using GradCast.Api.Models;
using GradCast.Data;
using GradCast.Data.Entities;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GradCast.Api.Tests;

public class ScenarioRepositoryTests
{
    private static async Task<(SqliteConnectionFactory Factory, string DbPath)> CreateTestDatabaseAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-scenarios-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(databasePath);

        await using var conn = await factory.CreateOpenConnectionAsync();
        await SqliteDatabaseInitializer.InitializeAsync(conn);

        // Seed basic reference data
        await conn.ExecuteAsync("""
            INSERT INTO schools (id, name, city, state, school_url, ownership) VALUES
            (101, 'University of California, Berkeley', 'Berkeley', 'CA', 'https://berkeley.edu', 1),
            (102, 'Stanford University', 'Stanford', 'CA', 'https://stanford.edu', 2);

            INSERT INTO cbsa_locations (cbsa_code, name, state, type) VALUES
            ('41860', 'San Francisco-Oakland-Berkeley, CA', 'CA', 'Metropolitan'),
            ('31080', 'Los Angeles-Long Beach-Anaheim, CA', 'CA', 'Metropolitan');
        """);

        return (factory, databasePath);
    }

    [Fact]
    public async Task InsertScenario_PersistsAndCanBeRetrievedById()
    {
        var (factory, dbPath) = await CreateTestDatabaseAsync();

        try
        {
            var repo = new ScenarioRepository(factory);
            var scenarioId = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow.ToString("o");

            var scenario = new SharedScenario
            {
                Id = scenarioId,
                CreatedAt = now,
                SchoolId = 101,
                CipCode = "11.0701",
                CredentialLevel = 3,
                CbsaCode = "41860",
                HousingType = "alone",
                SalaryMode = "scorecard_median",
                ReportedAnnualSalary = 120_000,
                MonthlyNetTakeHome = 7_200,
                MonthlyRent = 2_400,
                MonthlyLoanPayment = 350,
                MonthlyDisposable = 4_450,
                DisposableRatio = 0.618,
                SentimentTag = "comfortable",
                ScenarioTitle = "CS Grad in SF Bay Area",
                ClientHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"
            };

            var insertedId = await repo.InsertScenarioAsync(scenario);
            Assert.Equal(scenarioId, insertedId);

            var retrieved = await repo.GetByIdAsync(scenarioId);
            Assert.NotNull(retrieved);
            Assert.Equal(scenarioId, retrieved.Id);
            Assert.Equal("University of California, Berkeley", retrieved.SchoolName);
            Assert.Equal("San Francisco-Oakland-Berkeley, CA", retrieved.LocationName);
            Assert.Equal(4_450, retrieved.MonthlyDisposable);
            Assert.Equal("CS Grad in SF Bay Area", retrieved.ScenarioTitle);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task GetPositiveFinds_FiltersByCbsaAndCipFamily_AndOrdersByDisposable()
    {
        var (factory, dbPath) = await CreateTestDatabaseAsync();

        try
        {
            var repo = new ScenarioRepository(factory);

            // Insert 3 scenarios:
            // 1. High disposable, CS (11.0701), SF (41860)
            await repo.InsertScenarioAsync(new SharedScenario
            {
                Id = "scen-1",
                CreatedAt = "2026-10-14T10:00:00Z",
                SchoolId = 101,
                CipCode = "11.0701",
                CredentialLevel = 3,
                CbsaCode = "41860",
                HousingType = "alone",
                SalaryMode = "scorecard_median",
                ReportedAnnualSalary = 130_000,
                MonthlyNetTakeHome = 7_800,
                MonthlyRent = 2_200,
                MonthlyLoanPayment = 300,
                MonthlyDisposable = 5_300,
                DisposableRatio = 0.679,
                SentimentTag = "surplus_plus",
                ClientHash = "1111111111111111111111111111111111111111111111111111111111111111"
            });

            // 2. Medium disposable, CS (11.0101), SF (41860)
            await repo.InsertScenarioAsync(new SharedScenario
            {
                Id = "scen-2",
                CreatedAt = "2026-10-14T11:00:00Z",
                SchoolId = 101,
                CipCode = "11.0101",
                CredentialLevel = 3,
                CbsaCode = "41860",
                HousingType = "roommate",
                SalaryMode = "user_override",
                ReportedAnnualSalary = 95_000,
                MonthlyNetTakeHome = 5_900,
                MonthlyRent = 1_500,
                MonthlyLoanPayment = 400,
                MonthlyDisposable = 4_000,
                DisposableRatio = 0.677,
                SentimentTag = "comfortable",
                ClientHash = "2222222222222222222222222222222222222222222222222222222222222222"
            });

            // 3. Negative disposable, should NOT be returned by positive finds
            await repo.InsertScenarioAsync(new SharedScenario
            {
                Id = "scen-3",
                CreatedAt = "2026-10-14T12:00:00Z",
                SchoolId = 102,
                CipCode = "11.0701",
                CredentialLevel = 3,
                CbsaCode = "41860",
                HousingType = "alone",
                SalaryMode = "scorecard_median",
                ReportedAnnualSalary = 40_000,
                MonthlyNetTakeHome = 2_600,
                MonthlyRent = 2_400,
                MonthlyLoanPayment = 500,
                MonthlyDisposable = -300,
                DisposableRatio = -0.115,
                SentimentTag = "tight",
                ClientHash = "3333333333333333333333333333333333333333333333333333333333333333"
            });

            // 4. LA scenario, CS
            await repo.InsertScenarioAsync(new SharedScenario
            {
                Id = "scen-4",
                CreatedAt = "2026-10-14T13:00:00Z",
                SchoolId = 102,
                CipCode = "11.0701",
                CredentialLevel = 3,
                CbsaCode = "31080",
                HousingType = "alone",
                SalaryMode = "scorecard_median",
                ReportedAnnualSalary = 110_000,
                MonthlyNetTakeHome = 6_800,
                MonthlyRent = 2_100,
                MonthlyLoanPayment = 300,
                MonthlyDisposable = 4_400,
                DisposableRatio = 0.647,
                SentimentTag = "comfortable",
                ClientHash = "4444444444444444444444444444444444444444444444444444444444444444"
            });

            // Query SF positive finds
            var sfResults = await repo.GetPositiveFindsAsync(cbsaCode: "41860", cipFamily: "11");
            Assert.Equal(2, sfResults.Count);
            // Verify descending disposable order
            Assert.Equal("scen-1", sfResults[0].Id);
            Assert.Equal(5_300, sfResults[0].MonthlyDisposable);
            Assert.Equal("scen-2", sfResults[1].Id);
            Assert.Equal(4_000, sfResults[1].MonthlyDisposable);

            // Verify negative scenario was excluded
            Assert.DoesNotContain(sfResults, s => s.Id == "scen-3");

            // Query LA positive finds
            var laResults = await repo.GetPositiveFindsAsync(cbsaCode: "31080");
            Assert.Single(laResults);
            Assert.Equal("scen-4", laResults[0].Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task GetDailySubmissionCount_CountsSubmissionsAccurately()
    {
        var (factory, dbPath) = await CreateTestDatabaseAsync();

        try
        {
            var repo = new ScenarioRepository(factory);
            const string clientHash = "5555555555555555555555555555555555555555555555555555555555555555";
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            await repo.InsertScenarioAsync(new SharedScenario
            {
                Id = "sub-1",
                CreatedAt = $"{today}T08:00:00Z",
                SchoolId = 101,
                CipCode = "11.0701",
                CredentialLevel = 3,
                CbsaCode = "41860",
                HousingType = "alone",
                SalaryMode = "scorecard_median",
                ReportedAnnualSalary = 100_000,
                MonthlyNetTakeHome = 6_000,
                MonthlyRent = 2_000,
                MonthlyLoanPayment = 300,
                MonthlyDisposable = 3_700,
                DisposableRatio = 0.61,
                SentimentTag = "comfortable",
                ClientHash = clientHash
            });

            await repo.InsertScenarioAsync(new SharedScenario
            {
                Id = "sub-2",
                CreatedAt = $"{today}T09:00:00Z",
                SchoolId = 101,
                CipCode = "11.0701",
                CredentialLevel = 3,
                CbsaCode = "41860",
                HousingType = "alone",
                SalaryMode = "scorecard_median",
                ReportedAnnualSalary = 100_000,
                MonthlyNetTakeHome = 6_000,
                MonthlyRent = 2_000,
                MonthlyLoanPayment = 300,
                MonthlyDisposable = 3_700,
                DisposableRatio = 0.61,
                SentimentTag = "comfortable",
                ClientHash = clientHash
            });

            var count = await repo.GetDailySubmissionCountByClientHashAsync(clientHash, today);
            Assert.Equal(2, count);

            var otherCount = await repo.GetDailySubmissionCountByClientHashAsync("6666666666666666666666666666666666666666666666666666666666666666", today);
            Assert.Equal(0, otherCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
