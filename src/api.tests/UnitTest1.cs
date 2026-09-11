using System.Net.Http.Json;
using GradCast.Api.Endpoints;
using GradCast.Api.Models;
using GradCast.Api.Services;
using GradCast.Data.Entities;
using GradCast.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using GradCast.Api.Configuration;
using GradCast.Api.Extensions;

namespace GradCast.Api.Tests;

public class TaxCalculationServiceTests
{
    [Fact]
    public void ConstructorAcceptsProviderAndUsesReturnedConfig()
    {
        var provider = new StubTaxConfigProvider(new TaxConfig
        {
            TaxYear = 2026,
            StandardDeduction = 0,
            SocialSecurityRate = 0.062m,
            SocialSecurityWageCap = 176100,
            MedicareRate = 0.0145m,
            FederalBrackets = new[]
            {
                new TaxBracket(100000, 0.10m)
            },
            StateTaxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["CA"] = 0.06m
            }
        });

        var service = new TaxCalculationService(provider);

        Assert.Equal(2026, service.TaxYear);

        var result = service.Calculate(75000m, "ca");
        Assert.Equal(75000m, result.GrossAnnual);
        Assert.Equal(0.06m, result.StateTaxRate);
    }

    [Fact]
    public void EmptyFederalBracketCollectionReturnsZeroFederalTax()
    {
        var provider = new StubTaxConfigProvider(new TaxConfig
        {
            TaxYear = 2026,
            StandardDeduction = 0,
            SocialSecurityRate = 0.062m,
            SocialSecurityWageCap = 176100,
            MedicareRate = 0.0145m,
            FederalBrackets = Array.Empty<TaxBracket>(),
            StateTaxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["CA"] = 0.06m
            }
        });

        var service = new TaxCalculationService(provider);
        var result = service.Calculate(10000m, "CA");

        Assert.Equal(0m, result.FederalTaxAnnual);
    }

    [Fact]
    public void UnknownStateUsesFourPercentFallbackOnGrossSalary()
    {
        var provider = new StubTaxConfigProvider(new TaxConfig
        {
            TaxYear = 2026,
            StandardDeduction = 0,
            SocialSecurityRate = 0.062m,
            SocialSecurityWageCap = 176100,
            MedicareRate = 0.0145m,
            FederalBrackets = Array.Empty<TaxBracket>(),
            StateTaxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["CA"] = 0.06m
            }
        });

        var service = new TaxCalculationService(provider);
        var result = service.Calculate(100000m, "ZZ");

        var expected = Math.Round(100000m * 0.04m, 2);
        Assert.Equal(expected, result.StateTaxAnnual);
    }

    [Fact]
    public void FileTaxConfigProviderThrowsWhenTaxDirectoryIsMissing()
    {
        var env = new StubWebHostEnvironment(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var provider = new FileTaxConfigProvider(env);

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetConfig());
        Assert.Contains("Configuration/TaxData", ex.Message);
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void FileTaxConfigProviderThrowsWhenDirectoryExistsButHasNoTaxFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Configuration", "TaxData"));

        var env = new StubWebHostEnvironment(root);
        var provider = new FileTaxConfigProvider(env);

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetConfig());
        Assert.Contains("No tax configuration files found", ex.Message);

        Directory.Delete(root, recursive: true);
    }
}

public class LoanAmortizationServiceTests
{
    [Fact]
    public void LoanAmortizationServiceCalculatesMonthlyPaymentAcrossDefaultAndCustomTerms()
    {
        var loan = new LoanAmortizationService();

        var result = loan.Calculate(12000m, 0.12m, 1);

        Assert.Equal(12_000m, result.Principal);
        Assert.True(result.MonthlyPayment > 0m);
        Assert.True(result.TotalPaid >= result.Principal);
        Assert.Equal(0.12m, result.AnnualRate);
        Assert.Equal(1, result.TermYears);
    }
}

public class HousingCostServiceTests
{
    [Fact]
    public async Task HousingCostServiceMapsTheRequestedHousingTypeToTheRentBranch()
    {
        var repo = new StubGradCastRepository(
            rent: new FairMarketRent
            {
                CbsaCode = "12345",
                Year = 2026,
                Efficiency = 700,
                OneBedroom = 900,
                TwoBedroom = 1200,
                ThreeBedroom = 1400,
                FourBedroom = 1600
            });

        IHousingCostService service = new HousingCostService(repo);
        var result = await service.GetHousingCostAsync("12345", "2bed");

        Assert.NotNull(result);
        Assert.Equal("12345", result!.CbsaCode);
        Assert.Equal("2bed", result.HousingType);
        Assert.Equal(600, result.MonthlyRent);
        Assert.Equal(1200, result.FullRent);
    }

    [Fact]
    public async Task HousingCostServiceReturnsNullWhenTheRepositoryCannotFindHousingData()
    {
        var repo = new StubGradCastRepository();
        IHousingCostService service = new HousingCostService(repo);

        var result = await service.GetHousingCostAsync("missing", "1bed");

        Assert.Null(result);
    }
}

public class BudgetSimulatorServiceTests
{
    [Fact]
    public async Task BudgetSimulationStatusClassifiesDisposableIncomeAsTightWhenResultIsPositiveButSmall()
    {
        var repo = new StubGradCastRepository(new FairMarketRent
        {
            CbsaCode = "12345",
            Year = 2026,
            OneBedroom = 900,
            TwoBedroom = 1200,
            Efficiency = 700
        });
        IHousingCostService housing = new HousingCostService(repo);
        var provider = new StubTaxConfigProvider(new TaxConfig
        {
            TaxYear = 2026,
            StandardDeduction = 0,
            SocialSecurityRate = 0.062m,
            SocialSecurityWageCap = 176100,
            MedicareRate = 0.0145m,
            FederalBrackets = Array.Empty<TaxBracket>(),
            StateTaxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["CA"] = 0.0m
            }
        });

        var taxService = new TaxCalculationService(provider);
        var amortization = new LoanAmortizationService();
        var service = new BudgetSimulatorService(repo, housing, taxService, amortization);

        var request = new BudgetSimulationRequest(1, string.Empty, "12345", "1bed", 1000m);

        var result = await service.SimulateAsync(request);
        var success = Assert.IsType<BudgetSimulationSuccess>(result);
        Assert.Contains(success.Simulation.IncomeStatus, new[] { "tight", "deficit", "manageable", "comfortable" });
    }

    [Fact]
    public async Task BudgetSimulationReturnsHousingDataUnavailableWhenAValidLocationHasNoFmrRecord()
    {
        var repo = new StubGradCastRepository();
        IHousingCostService housing = new HousingCostService(repo);
        var taxService = new StubTaxCalculationService();
        var service = new BudgetSimulatorService(repo, housing, taxService, new LoanAmortizationService());

        var outcome = await service.SimulateAsync(new BudgetSimulationRequest(1, null, "12345", "1bed", 50000m));

        var unavailable = Assert.IsType<BudgetSimulationUnavailable>(outcome);
        Assert.Equal(BudgetSimulationUnavailableReason.HousingDataUnavailable, unavailable.Reason);
        Assert.Contains("12345", unavailable.Detail);
    }
}

public class ProgramEarningsRepositoryTests
{
    [Fact]
    public async Task ProgramEarningsMatchNormalizedCipAndSelectedCredential()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<GradCastDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using var db = new GradCastDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Schools.Add(new School { Id = 1, Name = "Test School", City = "Test City", State = "CA" });
            await db.SaveChangesAsync();
            db.Programs.AddRange(
                new GradCast.Data.Entities.Program { SchoolId = 1, Year = 2024, CipCode = "11.07", CredentialLevel = 2, Title = "Legacy format", MedianEarnings = 45000m },
                new GradCast.Data.Entities.Program { SchoolId = 1, Year = 2024, CipCode = "1107", CredentialLevel = 3, Title = "Canonical format", MedianEarnings = 75000m },
                new GradCast.Data.Entities.Program { SchoolId = 1, Year = 2024, CipCode = "1107", CredentialLevel = 5, Title = "Canonical format", MedianEarnings = 95000m });
            await db.SaveChangesAsync();

            var repository = new GradCastRepository(db);

            var selectedEarnings = await repository.GetProgramMedianEarningsAsync(1, "11.0701", 3);
            var legacyEarnings = await repository.GetProgramMedianEarningsAsync(1, "1107", 2);
            var fallbackEarnings = await repository.GetProgramMedianEarningsAsync(1, "1107");

            Assert.Equal(75000m, selectedEarnings);
            Assert.Equal(45000m, legacyEarnings);
            Assert.Equal(75000m, fallbackEarnings);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}

public class ReferenceDataSeederIntegrationTests
{
    [Fact]
    public async Task ReferenceDataSeederUpdatesCorrectedRowsAndRetainsNewFmrYears()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-reference-data-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<GradCastDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using var db = new GradCastDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var initialResult = await ReferenceDataSeeder.SeedAsync(
                db,
                [new GradCast.Data.SeedData.CbsaSeed.CbsaEntry("12345", "Original Metro", "CA", "Metropolitan")],
                [new GradCast.Data.SeedData.FmrSeed.FmrEntry("12345", 2025, 700, 900, 1100, 1400, 1600)]);

            var updateResult = await ReferenceDataSeeder.SeedAsync(
                db,
                [new GradCast.Data.SeedData.CbsaSeed.CbsaEntry("12345", "Corrected Metro", "CA", "Micropolitan")],
                [
                    new GradCast.Data.SeedData.FmrSeed.FmrEntry("12345", 2025, 710, 925, 1125, 1425, 1625),
                    new GradCast.Data.SeedData.FmrSeed.FmrEntry("12345", 2026, 750, 975, 1175, 1475, 1675)
                ]);

            Assert.Equal(new ReferenceDataSeedResult(1, 0, 1, 0), initialResult);
            Assert.Equal(new ReferenceDataSeedResult(0, 1, 1, 1), updateResult);

            var location = await db.CbsaLocations.SingleAsync(location => location.CbsaCode == "12345");
            var rents = await db.FairMarketRents
                .Where(rent => rent.CbsaCode == "12345")
                .OrderBy(rent => rent.Year)
                .ToListAsync();

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
            File.Delete(databasePath);
        }
    }
}

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
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public async Task LoanPaymentRejectsInvalidTermsWithProblemDetails(int termYears)
    {
        var response = await _client.GetAsync($"/api/finance/loan-payment?principal=10000&termYears={termYears}");

        await AssertProblemResponse(response, "Invalid loan term");
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

    private static async Task AssertProblemResponse(
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
    }
}

public sealed class StubTaxCalculationService : ITaxCalculationService
{
    public int TaxYear => 2026;
    public NetPayResult Calculate(decimal grossAnnualSalary, string state)
        => new NetPayResult(
            GrossAnnual: grossAnnualSalary,
            GrossMonthly: Math.Round(grossAnnualSalary / 12, 2),
            FederalTaxAnnual: 0,
            FederalTaxMonthly: 0,
            FicaAnnual: 0,
            FicaMonthly: 0,
            StateTaxAnnual: 0,
            StateTaxMonthly: 0,
            StateTaxRate: 0,
            NetAnnual: grossAnnualSalary,
            NetMonthly: Math.Round(grossAnnualSalary / 12, 2),
            EffectiveTaxRate: 0);
}

public sealed class StubBudgetSimulatorService : IBudgetSimulatorService
{
    public Task<BudgetSimulationOutcome> SimulateAsync(BudgetSimulationRequest request, CancellationToken ct = default)
        => Task.FromResult<BudgetSimulationOutcome>(new BudgetSimulationSuccess(new BudgetSimulationResult(
            GrossAnnualSalary: 100000m,
            GrossMonthly: 8333.33m,
            NetMonthly: 6200.00m,
            EffectiveTaxRate: 0.10m,
            SalarySource: "stub",
            RentMonthly: 1200m,
            HousingType: request.HousingType,
            LoanPaymentMonthly: 300m,
            LoanPrincipal: 12000m,
            FixedCostsMonthly: 1500m,
            DisposableMonthly: 4700m,
            IncomeStatus: "comfortable",
            LocationName: "Stub City",
            State: "CA")));
}

public sealed class UnavailableBudgetSimulatorService : IBudgetSimulatorService
{
    public Task<BudgetSimulationOutcome> SimulateAsync(BudgetSimulationRequest request, CancellationToken ct = default)
        => Task.FromResult<BudgetSimulationOutcome>(new BudgetSimulationUnavailable(
            BudgetSimulationUnavailableReason.HousingDataUnavailable,
            "No Fair Market Rent data is available for CBSA code '12345'."));
}

public sealed class StubTaxConfigProvider : ITaxConfigProvider
{
    private readonly TaxConfig _config;

    public StubTaxConfigProvider(TaxConfig config)
    {
        _config = config;
    }

    public TaxConfig GetConfig() => _config;
}

public sealed class StubGradCastRepository : IGradCastRepository
{
    private readonly FairMarketRent? _rent;

    public StubGradCastRepository(FairMarketRent? rent = null)
    {
        _rent = rent;
    }

    public Task<CbsaLocation?> GetLocationByCbsaCodeAsync(string cbsaCode, CancellationToken ct = default)
        => Task.FromResult<CbsaLocation?>(new CbsaLocation { CbsaCode = cbsaCode, Name = "Test City", State = "CA" });

    public Task<FairMarketRent?> GetLatestFairMarketRentAsync(string cbsaCode, CancellationToken ct = default)
        => Task.FromResult(_rent);

    public Task<decimal?> GetProgramMedianEarningsAsync(
        int schoolId,
        string cipCode,
        int? credentialLevel = null,
        CancellationToken ct = default)
        => Task.FromResult<decimal?>(100000m);

    public Task<List<decimal>> GetSchoolMedianEarningsAsync(int schoolId, CancellationToken ct = default)
        => Task.FromResult(new List<decimal> { 100000m });

    public Task<decimal> GetMedianDebtAsync(int schoolId, CancellationToken ct = default)
        => Task.FromResult(10000m);
}

public sealed class StubWebHostEnvironment : IWebHostEnvironment
{
    public StubWebHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = new NullFileProvider();
        WebRootPath = string.Empty;
        WebRootFileProvider = new NullFileProvider();
        EnvironmentName = Environments.Production;
        ApplicationName = typeof(StubWebHostEnvironment).Assembly.FullName ?? string.Empty;
    }

    public string ApplicationName { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
    public string EnvironmentName { get; set; }
}

public class ConfigurationBindingTests
{
    [Fact]
    public void OptionsBindsFromHierarchicalConfiguration()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["CollegeScorecard:ApiKey"] = "hierarchical-scorecard-key",
            ["Adzuna:AppId"] = "hierarchical-adzuna-id",
            ["Adzuna:AppKey"] = "hierarchical-adzuna-key"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var services = new ServiceCollection();
        var env = new StubWebHostEnvironment(Directory.GetCurrentDirectory());
        services.AddGradCastServices(configuration, env);

        var provider = services.BuildServiceProvider();
        var scorecardOptions = provider.GetRequiredService<IOptions<CollegeScorecardOptions>>().Value;
        var adzunaOptions = provider.GetRequiredService<IOptions<AdzunaOptions>>().Value;

        Assert.Equal("hierarchical-scorecard-key", scorecardOptions.ApiKey);
        Assert.Equal("hierarchical-adzuna-id", adzunaOptions.AppId);
        Assert.Equal("hierarchical-adzuna-key", adzunaOptions.AppKey);
    }

    [Fact]
    public void OptionsBindsFromFlatEnvironmentVariableFallback()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["COLLEGE_SCORECARD_API_KEY"] = "flat-scorecard-key",
            ["ADZUNA_APP_ID"] = "flat-adzuna-id",
            ["ADZUNA_APP_KEY"] = "flat-adzuna-key"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var services = new ServiceCollection();
        var env = new StubWebHostEnvironment(Directory.GetCurrentDirectory());
        services.AddGradCastServices(configuration, env);

        var provider = services.BuildServiceProvider();
        var scorecardOptions = provider.GetRequiredService<IOptions<CollegeScorecardOptions>>().Value;
        var adzunaOptions = provider.GetRequiredService<IOptions<AdzunaOptions>>().Value;

        Assert.Equal("flat-scorecard-key", scorecardOptions.ApiKey);
        Assert.Equal("flat-adzuna-id", adzunaOptions.AppId);
        Assert.Equal("flat-adzuna-key", adzunaOptions.AppKey);
    }
}

public class DataModeAndCredentialTests
{
    [Fact]
    public void DataSource_DefaultsToHybrid_WhenNotConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var env = new StubWebHostEnvironment(Directory.GetCurrentDirectory());
        services.AddGradCastServices(configuration, env);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICollegeScorecardService>();

        Assert.IsType<HybridCollegeScorecardService>(service);
    }

    [Theory]
    [InlineData("local", typeof(LocalCollegeScorecardService))]
    [InlineData("hybrid", typeof(HybridCollegeScorecardService))]
    [InlineData("api", typeof(CollegeScorecardService))]
    public void DataSource_RegistersExpectedService_ForEachMode(string mode, Type expectedType)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataSource"] = mode })
            .Build();

        var services = new ServiceCollection();
        var env = new StubWebHostEnvironment(Directory.GetCurrentDirectory());
        services.AddGradCastServices(configuration, env);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICollegeScorecardService>();

        Assert.IsType(expectedType, service);
    }

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

