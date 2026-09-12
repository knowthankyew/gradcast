using Dapper;
using GradCast.Api.Configuration;
using GradCast.Api.Services;
using GradCast.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace GradCast.Api.Tests;

public class CollegeScorecardServiceTests
{
    [Fact]
    public async Task CollegeScorecardService_ThrowsActionableException_WhenApiKeyMissing()
    {
        var options = Options.Create(new CollegeScorecardOptions
        {
            ApiKey = "",
            BaseUrl = "https://api.data.gov/ed/collegescorecard/v1"
        });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = new LoggerFactory().CreateLogger<CollegeScorecardService>();
        var client = new HttpClient();

        var service = new CollegeScorecardService(client, cache, options, logger);

        var ex = await Assert.ThrowsAsync<CollegeScorecardMissingApiKeyException>(() =>
            service.SearchSchoolsAsync("Harvard", null));

        Assert.Contains("College Scorecard API key is not configured", ex.Message);
        Assert.Contains("CollegeScorecard:ApiKey", ex.Message);
        Assert.Contains("COLLEGE_SCORECARD_API_KEY", ex.Message);
    }

    [Fact]
    public async Task HybridCollegeScorecardService_ServesLocalData_WithoutApiKey()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"hybrid_no_key_test_{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);

        try
        {
            await using (var conn = await factory.CreateOpenConnectionAsync())
            {
                await SqliteDatabaseInitializer.InitializeAsync(conn);

                await conn.ExecuteAsync("""
                    INSERT INTO schools (id, name, city, state, school_url, ownership) VALUES
                    (99999, 'Local University', 'Austin', 'TX', NULL, 1);
                """);
            }

            var localService = new LocalCollegeScorecardService(factory, new LoggerFactory().CreateLogger<LocalCollegeScorecardService>());

            var emptyScorecardOptions = Options.Create(new CollegeScorecardOptions
            {
                ApiKey = "",
                BaseUrl = "https://api.data.gov/ed/collegescorecard/v1"
            });
            var remoteService = new CollegeScorecardService(
                new HttpClient(),
                new MemoryCache(new MemoryCacheOptions()),
                emptyScorecardOptions,
                new LoggerFactory().CreateLogger<CollegeScorecardService>());

            var hybridService = new HybridCollegeScorecardService(
                localService,
                remoteService,
                new LoggerFactory().CreateLogger<HybridCollegeScorecardService>());

            var results = await hybridService.SearchSchoolsAsync("Local", null);
            Assert.Single(results);
            Assert.Equal("Local University", results[0].Name);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
