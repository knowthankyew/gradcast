using System.Net.Http.Json;
using GradCast.Api.Endpoints;
using GradCast.Api.Models;
using GradCast.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GradCast.Api.Tests;

public class FinanceEndpointValidationContractTests
{
    [Fact]
    public void FinanceEndpointValidationFailureKeepsAStableProblemShape()
    {
        var request = new LoanPaymentRequest(-1m, 0.02m, null);
        var error = FinanceEndpoints.ValidateLoanRequest(request);

        Assert.NotNull(error);
        var result = error as IResult;
        Assert.NotNull(result);
    }
}

public class FinanceEndpointSmokeTests
{
    [Fact]
    public void FinanceRoutesExposeTheExpectedRouteContract()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton<ITaxCalculationService>(new StubTaxCalculationService());
        builder.Services.AddSingleton<ILoanAmortizationService>(new LoanAmortizationService());
        builder.Services.AddSingleton<IBudgetSimulatorService>(new StubBudgetSimulatorService());

        var app = builder.Build();

        app.MapFinanceEndpoints();

        IEndpointRouteBuilder routeBuilder = app;
        var endpoints = routeBuilder.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var routePaths = endpoints
            .Select(e => e.RoutePattern.RawText)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("/api/finance/net-pay", routePaths);
        Assert.Contains("/api/finance/loan-payment", routePaths);
        Assert.Contains("/api/finance/simulator", routePaths);

        var netPayEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/api/finance/net-pay");
        var loanEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/api/finance/loan-payment");
        var simulationEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/api/finance/simulator");

        Assert.Contains(netPayEndpoint.Metadata, m => m is HttpMethodMetadata metadata && metadata.HttpMethods.Contains("GET"));
        Assert.Contains(loanEndpoint.Metadata, m => m is HttpMethodMetadata metadata && metadata.HttpMethods.Contains("GET"));
        Assert.Contains(simulationEndpoint.Metadata, m => m is HttpMethodMetadata metadata && metadata.HttpMethods.Contains("POST"));
    }
}

public class FinanceEndpointHttpValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FinanceEndpointHttpValidationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITaxCalculationService>();
                services.AddSingleton<ITaxCalculationService>(new StubTaxCalculationService());
                services.RemoveAll<ILoanAmortizationService>();
                services.AddSingleton<ILoanAmortizationService>(new LoanAmortizationService());
                services.RemoveAll<IBudgetSimulatorService>();
                services.AddSingleton<IBudgetSimulatorService>(new StubBudgetSimulatorService());
            });
        }).CreateClient();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(10000001)]
    public async Task NetPayRejectsInvalidSalaryWithProblemDetails(decimal salary)
    {
        var response = await _client.GetAsync($"/api/finance/net-pay?grossSalary={salary}&state=CA");
        await AssertProblemResponse(response, "Invalid salary");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public async Task NetPayRejectsInvalidStateWithProblemDetails(string state)
    {
        var response = await _client.GetAsync($"/api/finance/net-pay?grossSalary=75000&state={Uri.EscapeDataString(state)}");
        await AssertProblemResponse(response, "Invalid state");
    }

    [Fact]
    public async Task NetPayReturnsSuccessfulNetPayResult()
    {
        var response = await _client.GetAsync("/api/finance/net-pay?grossSalary=75000&state=CA");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<NetPayResult>();
        Assert.NotNull(result);
        Assert.Equal(75000m, result!.GrossAnnual);
        Assert.Equal(6250.00m, result.GrossMonthly);
        Assert.Equal(75000m, result.NetAnnual);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000001)]
    public async Task LoanPaymentRejectsInvalidPrincipalWithProblemDetails(decimal principal)
    {
        var response = await _client.GetAsync($"/api/finance/loan-payment?principal={principal}&rate=0.05&termYears=10");
        await AssertProblemResponse(response, "Invalid principal");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.31)]
    public async Task LoanPaymentRejectsInvalidRateWithProblemDetails(decimal rate)
    {
        var response = await _client.GetAsync($"/api/finance/loan-payment?principal=10000&rate={rate}&termYears=10");
        await AssertProblemResponse(response, "Invalid rate");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public async Task LoanPaymentRejectsInvalidTermsWithProblemDetails(int termYears)
    {
        var response = await _client.GetAsync($"/api/finance/loan-payment?principal=10000&termYears={termYears}");

        await AssertProblemResponse(response, "Invalid loan term");
    }

    [Fact]
    public async Task LoanPaymentReturnsSuccessfulLoanPaymentResult()
    {
        var response = await _client.GetAsync("/api/finance/loan-payment?principal=12000&rate=0.12&termYears=1");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoanPaymentResult>();
        Assert.NotNull(result);
        Assert.Equal(12000m, result!.Principal);
        Assert.Equal(1066.19m, result.MonthlyPayment);
        Assert.Equal(12794.23m, result.TotalPaid);
        Assert.Equal(794.23m, result.TotalInterest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SimulatorRejectsInvalidSchoolWithProblemDetails(int schoolId)
    {
        var response = await _client.PostAsJsonAsync("/api/finance/simulator", new
        {
            schoolId,
            cbsaCode = "12345",
            housingType = "1bed"
        });

        await AssertProblemResponse(response, "Invalid school");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SimulatorRejectsMissingLocationWithProblemDetails(string cbsaCode)
    {
        var response = await _client.PostAsJsonAsync("/api/finance/simulator", new
        {
            schoolId = 1,
            cbsaCode,
            housingType = "1bed"
        });

        await AssertProblemResponse(response, "Invalid location");
    }

    [Fact]
    public async Task SimulatorRejectsUnsupportedHousingTypeWithProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/finance/simulator", new
        {
            schoolId = 1,
            cbsaCode = "12345",
            housingType = "penthouse"
        });

        await AssertProblemResponse(response, "Invalid housing type");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(10000001)]
    public async Task SimulatorRejectsInvalidSalaryOverrideWithProblemDetails(decimal salaryOverride)
    {
        var response = await _client.PostAsJsonAsync("/api/finance/simulator", new
        {
            schoolId = 1,
            cbsaCode = "12345",
            housingType = "1bed",
            salaryOverride
        });

        await AssertProblemResponse(response, "Invalid salary override");
    }

    [Fact]
    public async Task SimulatorKeepsTheSuccessfulBudgetResponseShape()
    {
        var response = await _client.PostAsJsonAsync("/api/finance/simulator", new
        {
            schoolId = 1,
            cbsaCode = "12345",
            housingType = "1bed"
        });

        response.EnsureSuccessStatusCode();
        var simulation = await response.Content.ReadFromJsonAsync<BudgetSimulationResult>();
        Assert.NotNull(simulation);
        Assert.Equal(1200m, simulation!.RentMonthly);
    }

    internal static async Task AssertProblemResponse(
        HttpResponseMessage response,
        string expectedTitle,
        System.Net.HttpStatusCode expectedStatus = System.Net.HttpStatusCode.BadRequest)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus} but received {(int)response.StatusCode}: {responseBody}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(expectedTitle, problem!.Title);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }
}

public class FinanceEndpointUnavailableDataTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FinanceEndpointUnavailableDataTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBudgetSimulatorService>();
                services.AddSingleton<IBudgetSimulatorService>(new UnavailableBudgetSimulatorService());
            });
        }).CreateClient();
    }

    [Fact]
    public async Task SimulatorReportsMissingHousingAsAnUnavailableDataProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/finance/simulator", new
        {
            schoolId = 1,
            cbsaCode = "12345",
            housingType = "1bed"
        });

        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Housing data unavailable", problem!.Title);
        Assert.Equal(422, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    [Fact]
    public async Task SimulatorReportsMissingLocationAsAnUnavailableDataProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/finance/simulator", new
        {
            schoolId = 1,
            cbsaCode = "99999",
            housingType = "1bed"
        });

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Location not found", problem!.Title);
        Assert.Equal(404, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }
}
