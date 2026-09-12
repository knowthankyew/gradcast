using Dapper;
using GradCast.Data;
using GradCast.Data.Entities;
using Xunit;

namespace GradCast.Api.Tests;

public class ReferenceDataSeederIntegrationTests
{
    [Fact]
    public async Task ReferenceDataSeederUpdatesCorrectedRowsAndRetainsNewFmrYears()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-reference-data-{Guid.NewGuid():N}.db");

        try
        {
            var factory = new SqliteConnectionFactory(databasePath);
            await using var connection = await factory.CreateOpenConnectionAsync();
            await SqliteDatabaseInitializer.InitializeAsync(connection);

            var initialResult = await ReferenceDataSeeder.SeedAsync(
                connection,
                [new GradCast.Data.SeedData.CbsaSeed.CbsaEntry("12345", "Original Metro", "CA", "Metropolitan")],
                [new GradCast.Data.SeedData.FmrSeed.FmrEntry("12345", 2025, 700, 900, 1100, 1400, 1600)]);

            var updateResult = await ReferenceDataSeeder.SeedAsync(
                connection,
                [new GradCast.Data.SeedData.CbsaSeed.CbsaEntry("12345", "Corrected Metro", "CA", "Micropolitan")],
                [
                    new GradCast.Data.SeedData.FmrSeed.FmrEntry("12345", 2025, 710, 925, 1125, 1425, 1625),
                    new GradCast.Data.SeedData.FmrSeed.FmrEntry("12345", 2026, 750, 975, 1175, 1475, 1675)
                ]);

            Assert.Equal(new ReferenceDataSeedResult(1, 0, 1, 0), initialResult);
            Assert.Equal(new ReferenceDataSeedResult(0, 1, 1, 1), updateResult);

            var location = await connection.QuerySingleAsync<CbsaLocation>(
                "SELECT cbsa_code, name, state, type FROM cbsa_locations WHERE cbsa_code = '12345';");

            var rents = (await connection.QueryAsync<FairMarketRent>(
                "SELECT id, cbsa_code, year, efficiency, one_bedroom, two_bedroom, three_bedroom, four_bedroom FROM fair_market_rents WHERE cbsa_code = '12345' ORDER BY year ASC;")).ToList();

            Assert.Equal("Corrected Metro", location.Name);
            Assert.Equal("Micropolitan", location.Type);
            Assert.Collection(
                rents,
                rent =>
                {
                    Assert.Equal(2025, rent.Year);
                    Assert.Equal(925, rent.OneBedroom);
                },
                rent =>
                {
                    Assert.Equal(2026, rent.Year);
                    Assert.Equal(975, rent.OneBedroom);
                });
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
