using System.Net;
using System.Net.Http.Json;
using GradCast.Api.Data;
using GradCast.Api.Endpoints;
using GradCast.Api.Models;
using GradCast.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GradCast.Api.Tests;

public class ScenarioEndpointValidationTests
{
    private static ShareScenarioRequest CreateValidRequest() => new(
        SchoolId: 1,
        CipCode: "11.0701",
        CredentialLevel: 3,
        CbsaCode: "41860",
        HousingType: "alone",
        SalaryMode: "scorecard_median",
        ReportedAnnualSalary: 85_000,
        MonthlyNetTakeHome: 5_200,
        MonthlyRent: 1_800,
        MonthlyLoanPayment: 300,
        MonthlyDisposable: 3_100,
        DisposableRatio: 0.596,
        SentimentTag: "comfortable",
        ScenarioTitle: "Valid Title"
    );

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = CreateValidRequest();
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void NegativeSalary_IsRejected(int negativeSalary)
    {
        var request = CreateValidRequest() with { ReportedAnnualSalary = negativeSalary };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(-20_001)]
    [InlineData(-50_000)]
    public void DisposableBelowNegativeTwentyThousand_IsRejected(int severeNegativeDisposable)
    {
        var request = CreateValidRequest() with { MonthlyDisposable = severeNegativeDisposable };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Fact]
    public void DisposableAtNegativeTwentyThousand_IsAccepted()
    {
        var request = CreateValidRequest() with
        {
            MonthlyNetTakeHome = 0,
            MonthlyRent = 20_000,
            MonthlyLoanPayment = 0,
            MonthlyDisposable = -20_000
        };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.Null(error);
    }

    [Fact]
    public void NegativeTakeHomePay_IsRejected()
    {
        var request = CreateValidRequest() with { MonthlyNetTakeHome = -1, MonthlyDisposable = -2_101 };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Fact]
    public void NegativeRent_IsRejected()
    {
        var request = CreateValidRequest() with { MonthlyRent = -1, MonthlyDisposable = 4_901 };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Fact]
    public void NegativeLoanPayment_IsRejected()
    {
        var request = CreateValidRequest() with { MonthlyLoanPayment = -1, MonthlyDisposable = 3_401 };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Fact]
    public void InconsistentDisposable_IsRejected()
    {
        var request = CreateValidRequest() with { MonthlyDisposable = 9_999 };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void InvalidSchoolId_IsRejected(int invalidSchoolId)
    {
        var request = CreateValidRequest() with { SchoolId = invalidSchoolId };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingCipCode_IsRejected(string? invalidCip)
    {
        var request = CreateValidRequest() with { CipCode = invalidCip! };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingCbsaCode_IsRejected(string? invalidCbsa)
    {
        var request = CreateValidRequest() with { CbsaCode = invalidCbsa! };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void InvalidCredentialLevel_IsRejected(int invalidCredential)
    {
        var request = CreateValidRequest() with { CredentialLevel = invalidCredential };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }

    [Fact]
    public void OverlyLongScenarioTitle_IsRejected()
    {
        var longTitle = new string('A', 61);
        var request = CreateValidRequest() with { ScenarioTitle = longTitle };
        var error = ScenarioEndpoints.ValidateShareRequest(request);
        Assert.NotNull(error);
    }
}

public class ScenarioEndpointClientHashTests
{
    [Fact]
    public void ComputeClientHash_ProducesDeterministicSha256Hex()
    {
        var hash1 = ScenarioEndpoints.ComputeClientHash("192.168.1.100", "test-salt");
        var hash2 = ScenarioEndpoints.ComputeClientHash("192.168.1.100", "test-salt");
        var hashDifferentIp = ScenarioEndpoints.ComputeClientHash("192.168.1.101", "test-salt");

        Assert.Equal(64, hash1.Length);
        Assert.Equal(hash1, hash2);
        Assert.NotEqual(hash1, hashDifferentIp);
    }

    [Fact]
    public void ComputeClientHash_DefaultFallbackSalt_ProducesValidSha256()
    {
        var hash = ScenarioEndpoints.ComputeClientHash("10.0.0.1");
        Assert.Equal(64, hash.Length);
        Assert.Matches(@"^[a-f0-9]{64}$", hash);
    }

    [Fact]
    public void ExtractClientIp_PrefersForwardedForHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.195, 70.41.3.18";
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var extracted = ScenarioEndpoints.ExtractClientIp(context);
        Assert.Equal("203.0.113.195", extracted);
    }

    [Fact]
    public void ExtractClientIp_FallsBackToRemoteIp_WhenNoForwardedHeader()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");

        var extracted = ScenarioEndpoints.ExtractClientIp(context);
        Assert.Equal("10.0.0.5", extracted);
    }
}

public class ScenarioEndpointSmokeTests
{
    [Fact]
    public void ScenarioRoutesExposeTheExpectedRouteContract()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IScenarioRepository>(new StubScenarioRepository());

        var app = builder.Build();
        app.MapScenarioEndpoints();

        IEndpointRouteBuilder routeBuilder = app;
        var endpoints = routeBuilder.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var routePaths = endpoints
            .Select(e => e.RoutePattern.RawText)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("/api/scenarios/share", routePaths);
        Assert.Contains("/api/scenarios/positive-finds", routePaths);
        Assert.Contains("/api/scenarios/{id}", routePaths);

        var shareEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/api/scenarios/share");
        Assert.Contains(shareEndpoint.Metadata, m => m is HttpMethodMetadata metadata && metadata.HttpMethods.Contains("POST"));

        var findsEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/api/scenarios/positive-finds");
        Assert.Contains(findsEndpoint.Metadata, m => m is HttpMethodMetadata metadata && metadata.HttpMethods.Contains("GET"));
    }
}

public class ScenarioEndpointHttpIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly StubScenarioRepository _stubRepo = new();

    public ScenarioEndpointHttpIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IScenarioRepository>();
                services.AddSingleton<IScenarioRepository>(_stubRepo);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task ShareScenario_ValidPayload_ReturnsCreatedWithId()
    {
        var request = new ShareScenarioRequest(
            SchoolId: 1,
            CipCode: "11.0701",
            CredentialLevel: 3,
            CbsaCode: "41860",
            HousingType: "alone",
            SalaryMode: "scorecard_median",
            ReportedAnnualSalary: 90_000,
            MonthlyNetTakeHome: 5_500,
            MonthlyRent: 2_000,
            MonthlyLoanPayment: 300,
            MonthlyDisposable: 3_200,
            DisposableRatio: 0.58,
            SentimentTag: "comfortable",
            ScenarioTitle: "Great CS Deal"
        );

        var response = await _client.PostAsJsonAsync("/api/scenarios/share", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ShareScenarioResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Id);
        Assert.Equal("Scenario shared successfully.", result.Message);
    }

    [Fact]
    public async Task ShareScenario_NegativeSalary_ReturnsBadRequest()
    {
        var request = new ShareScenarioRequest(
            SchoolId: 1,
            CipCode: "11.0701",
            CredentialLevel: 3,
            CbsaCode: "41860",
            HousingType: "alone",
            SalaryMode: "scorecard_median",
            ReportedAnnualSalary: -50_000,
            MonthlyNetTakeHome: 0,
            MonthlyRent: 2_000,
            MonthlyLoanPayment: 300,
            MonthlyDisposable: -2_300,
            DisposableRatio: 0,
            SentimentTag: "tight",
            ScenarioTitle: null
        );

        var response = await _client.PostAsJsonAsync("/api/scenarios/share", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid salary", problem!.Title);
    }

    [Fact]
    public async Task ShareScenario_SevereNegativeDisposable_ReturnsBadRequest()
    {
        var request = new ShareScenarioRequest(
            SchoolId: 1,
            CipCode: "11.0701",
            CredentialLevel: 3,
            CbsaCode: "41860",
            HousingType: "alone",
            SalaryMode: "scorecard_median",
            ReportedAnnualSalary: 50_000,
            MonthlyNetTakeHome: 3_000,
            MonthlyRent: 20_000,
            MonthlyLoanPayment: 5_000,
            MonthlyDisposable: -22_000,
            DisposableRatio: -7.33,
            SentimentTag: "tight",
            ScenarioTitle: null
        );

        var response = await _client.PostAsJsonAsync("/api/scenarios/share", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid disposable income", problem!.Title);
    }

    [Fact]
    public async Task GetPositiveFinds_ReturnsScenarioList()
    {
        var response = await _client.GetAsync("/api/scenarios/positive-finds?cbsaCode=41860");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<SharedScenarioDto>>();
        Assert.NotNull(items);
    }

    [Theory]
    [InlineData("invalid-cbsa", "11")]
    [InlineData("41860", "1")]
    [InlineData("41860", "111")]
    [InlineData("41860", "ab")]
    public async Task GetPositiveFinds_InvalidParameters_ReturnsBadRequest(string cbsaCode, string cipFamily)
    {
        var response = await _client.GetAsync($"/api/scenarios/positive-finds?cbsaCode={cbsaCode}&cipFamily={cipFamily}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetScenarioById_ReturnsNotFound_WhenNotExists()
    {
        var response = await _client.GetAsync("/api/scenarios/non-existent-id-999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class StubScenarioRepository : IScenarioRepository
{
    private readonly List<SharedScenario> _scenarios = new();

    public Task<string> InsertScenarioAsync(SharedScenario scenario, CancellationToken ct = default)
    {
        lock (_scenarios)
        {
            _scenarios.Add(scenario);
            return Task.FromResult(scenario.Id);
        }
    }

    public Task<SharedScenarioDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        lock (_scenarios)
        {
            var found = _scenarios.FirstOrDefault(s => s.Id == id);
            if (found == null) return Task.FromResult<SharedScenarioDto?>(null);

            return Task.FromResult<SharedScenarioDto?>(new SharedScenarioDto(
                found.Id,
                found.CreatedAt,
                found.SchoolId,
                "Stub School",
                found.CipCode,
                found.CredentialLevel,
                found.CbsaCode,
                "Stub Location",
                found.HousingType,
                found.SalaryMode,
                found.ReportedAnnualSalary,
                found.MonthlyNetTakeHome,
                found.MonthlyRent,
                found.MonthlyLoanPayment,
                found.MonthlyDisposable,
                found.DisposableRatio,
                found.SentimentTag,
                found.ScenarioTitle
            ));
        }
    }

    public Task<IReadOnlyList<SharedScenarioDto>> GetPositiveFindsAsync(
        string? cbsaCode = null,
        string? cipFamily = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        lock (_scenarios)
        {
            var items = _scenarios
                .Where(s => s.MonthlyDisposable > 0)
                .Where(s => cbsaCode == null || s.CbsaCode == cbsaCode)
                .Select(s => new SharedScenarioDto(
                    s.Id,
                    s.CreatedAt,
                    s.SchoolId,
                    "Stub School",
                    s.CipCode,
                    s.CredentialLevel,
                    s.CbsaCode,
                    "Stub Location",
                    s.HousingType,
                    s.SalaryMode,
                    s.ReportedAnnualSalary,
                    s.MonthlyNetTakeHome,
                    s.MonthlyRent,
                    s.MonthlyLoanPayment,
                    s.MonthlyDisposable,
                    s.DisposableRatio,
                    s.SentimentTag,
                    s.ScenarioTitle
                ))
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<SharedScenarioDto>>(items);
        }
    }

    public Task<int> GetDailySubmissionCountByClientHashAsync(string clientHash, string datePrefixUtc, CancellationToken ct = default)
    {
        lock (_scenarios)
        {
            var count = _scenarios.Count(s => s.ClientHash == clientHash && s.CreatedAt.StartsWith(datePrefixUtc));
            return Task.FromResult(count);
        }
    }

    public Task<bool> TryInsertScenarioWithDailyLimitAsync(SharedScenario scenario, int maxDailyLimit, string datePrefixUtc, CancellationToken ct = default)
    {
        lock (_scenarios)
        {
            var count = _scenarios.Count(s => s.ClientHash == scenario.ClientHash && s.CreatedAt.StartsWith(datePrefixUtc));
            if (count >= maxDailyLimit)
            {
                return Task.FromResult(false);
            }
            _scenarios.Add(scenario);
            return Task.FromResult(true);
        }
    }
}
