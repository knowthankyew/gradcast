using System.Net;
using System.Text;
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

    [Fact]
    public async Task LocalCollegeScorecardService_CorrectlyParsesTextMedianEarningsAsDecimal()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"local_earnings_test_{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);

        try
        {
            await using (var conn = await factory.CreateOpenConnectionAsync())
            {
                await SqliteDatabaseInitializer.InitializeAsync(conn);

                await conn.ExecuteAsync("""
                    INSERT INTO schools (id, name, city, state, school_url, ownership) VALUES
                    (482477, 'DeVry University-Illinois', 'Lisle', 'IL', NULL, 3);
                """);

                await conn.ExecuteAsync("""
                    INSERT INTO programs (school_id, year, cip_code, title, credential_level, completions, median_earnings) VALUES
                    (482477, 2024, '11.05', 'Computer Systems Analysis', 3, 153, '47609.0');
                """);
            }

            var service = new LocalCollegeScorecardService(factory, new LoggerFactory().CreateLogger<LocalCollegeScorecardService>());

            var detail = await service.GetSchoolDetailAsync(482477, 2024);
            Assert.NotNull(detail);
            Assert.Equal("DeVry University-Illinois", detail.Name);
            Assert.Single(detail.Programs);

            var program = detail.Programs[0];
            Assert.Equal("Computer Systems Analysis", program.Title);
            Assert.NotNull(program.MedianEarnings);
            Assert.Equal(47609m, program.MedianEarnings.Value);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task LocalCollegeScorecardService_SupportsUnanchoredTokensEnrollmentRankingAndExactQuotes()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"local_search_test_{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath);

        try
        {
            await using (var conn = await factory.CreateOpenConnectionAsync())
            {
                await SqliteDatabaseInitializer.InitializeAsync(conn);

                // Insert 12 A-D small career schools with "Texas" so old ORDER BY name ASC LIMIT 10 would have buried major universities
                for (var i = 1; i <= 12; i++)
                {
                    var id = 1000 + i;
                    await conn.ExecuteAsync(
                        "INSERT INTO schools (id, name, city, state, school_url, ownership) VALUES (@Id, @Name, 'Dallas', 'TX', NULL, 3);",
                        new { Id = id, Name = $"Academy #{i:D2} of Texas" });
                    await conn.ExecuteAsync(
                        "INSERT INTO school_year_data (school_id, year, student_size) VALUES (@Id, 2024, 150);",
                        new { Id = id });
                }

                await conn.ExecuteAsync("""
                    INSERT INTO schools (id, name, city, state, school_url, ownership) VALUES
                    (2001, 'The University of Texas at Austin', 'Austin', 'TX', NULL, 1),
                    (2002, 'Texas A&M University', 'College Station', 'TX', NULL, 1),
                    (2003, 'Texas Tech University', 'Lubbock', 'TX', NULL, 1),
                    (2004, 'Texas College', 'Tyler', 'TX', NULL, 2),
                    (2005, 'Central Texas College', 'Killeen', 'TX', NULL, 1),
                    (2006, 'University of Massachusetts-Dartmouth', 'North Dartmouth', 'MA', NULL, 1),
                    (2007, 'Academy of Art University', 'San Francisco', 'CA', NULL, 3),
                    (2008, '100% Technical_Institute', 'Houston', 'TX', NULL, 3);

                    INSERT INTO school_year_data (school_id, year, student_size) VALUES
                    (2001, 2024, 42855),
                    (2002, 2024, 59615),
                    (2003, 2024, 32394),
                    (2004, 2024, 614),
                    (2005, 2024, 6247),
                    (2006, 2024, 5221),
                    (2007, 2024, 3587),
                    (2008, 2024, 400);
                """);
            }

            var service = new LocalCollegeScorecardService(factory, new LoggerFactory().CreateLogger<LocalCollegeScorecardService>());

            // 1. Enrollment ranking surfaces major flagship universities regardless of whether "Texas" is at index 0
            var texasResults = await service.SearchSchoolsAsync("Texas", null);
            Assert.True(texasResults.Count > 10);
            Assert.Equal("Texas A&M University", texasResults[0].Name);
            Assert.Equal("The University of Texas at Austin", texasResults[1].Name);
            Assert.Equal("Texas Tech University", texasResults[2].Name);

            // 2. Multi-token unanchored search matches non-adjacent words
            var multiTokenResults = await service.SearchSchoolsAsync("Texas Austin", null);
            Assert.Single(multiTokenResults);
            Assert.Equal("The University of Texas at Austin", multiTokenResults[0].Name);

            // 3. Mid-word fragment + city search matches across name and city columns
            var midWordCityResults = await service.SearchSchoolsAsync("ech lubbock", null);
            Assert.Single(midWordCityResults);
            Assert.Equal("Texas Tech University", midWordCityResults[0].Name);

            // 4. Double-quoted exact phrase ranks exact full-name match #1 and excludes non-contiguous matches
            var exactCollegeResults = await service.SearchSchoolsAsync("\"Texas College\"", null);
            Assert.Equal(2, exactCollegeResults.Count);
            Assert.Equal("Texas College", exactCollegeResults[0].Name);
            Assert.Equal("Central Texas College", exactCollegeResults[1].Name);

            // 5. Double-quoted exact word excludes mid-word substring matches ("art" vs "Dartmouth")
            var unquotedArt = await service.SearchSchoolsAsync("art", null);
            Assert.Equal(2, unquotedArt.Count);

            var quotedArt = await service.SearchSchoolsAsync("\"art\"", null);
            Assert.Single(quotedArt);
            Assert.Equal("Academy of Art University", quotedArt[0].Name);

            // 6. SQL LIKE wildcards (% and _) in user input are escaped literally
            var literalPercent = await service.SearchSchoolsAsync("100%", null);
            Assert.Single(literalPercent);
            Assert.Equal("100% Technical_Institute", literalPercent[0].Name);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task CollegeScorecardService_StripsQuotesUpstreamAndFiltersExactPhrasesLocally()
    {
        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedUri = request.RequestUri;
            const string payload = """
                {
                  "results": [
                    { "id": 1, "school.name": "University of Massachusetts-Dartmouth", "school.city": "North Dartmouth", "school.state": "MA" },
                    { "id": 2, "school.name": "Academy of Art University", "school.city": "San Francisco", "school.state": "CA" }
                  ]
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var options = Options.Create(new CollegeScorecardOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.data.gov/ed/collegescorecard/v1"
        });
        var service = new CollegeScorecardService(
            new HttpClient(handler),
            new MemoryCache(new MemoryCacheOptions()),
            options,
            new LoggerFactory().CreateLogger<CollegeScorecardService>());

        var results = await service.SearchSchoolsAsync("\"art\"", null);

        Assert.NotNull(capturedUri);
        Assert.Contains("school.name=art", capturedUri!.Query);
        Assert.DoesNotContain("%22", capturedUri.Query);
        Assert.Contains("per_page=25", capturedUri.Query);
        Assert.Single(results);
        Assert.Equal("Academy of Art University", results[0].Name);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
