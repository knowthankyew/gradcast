using GradCast.Api.Configuration;
using GradCast.Api.Services;
using GradCast.Data;
using GradCast.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        var dbName = $"hybrid_no_key_test_{Guid.NewGuid():N}.db";
        var optionsBuilder = new DbContextOptionsBuilder<GradCastDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbName}");

        await using var db = new GradCastDbContext(optionsBuilder.Options);
        await db.Database.EnsureCreatedAsync();

        try
        {
            db.Schools.Add(new School
            {
                Id = 99999,
                Name = "Local University",
                City = "Austin",
                State = "TX"
            });
            await db.SaveChangesAsync();

            var localService = new LocalCollegeScorecardService(db, new LoggerFactory().CreateLogger<LocalCollegeScorecardService>());

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
                db,
                new LoggerFactory().CreateLogger<HybridCollegeScorecardService>());

            var results = await hybridService.SearchSchoolsAsync("Local", null);
            Assert.Single(results);
            Assert.Equal("Local University", results[0].Name);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
