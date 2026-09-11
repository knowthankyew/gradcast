using GradCast.Api.Endpoints;
using GradCast.Api.Models;
using GradCast.Api.Services;
using GradCast.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

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
    public void HousingCostServiceMapsTheRequestedHousingTypeToTheRentBranch()
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
        var result = service.GetHousingCostAsync("12345", "2bed").GetAwaiter().GetResult();

        Assert.NotNull(result);
        Assert.Equal("12345", result!.CbsaCode);
        Assert.Equal("2bed", result.HousingType);
        Assert.Equal(600, result.MonthlyRent);
        Assert.Equal(1200, result.FullRent);
    }

    [Fact]
    public void HousingCostServiceReturnsNullWhenTheRepositoryCannotFindHousingData()
    {
        var repo = new StubGradCastRepository();
        IHousingCostService service = new HousingCostService(repo);

        var result = service.GetHousingCostAsync("missing", "1bed").GetAwaiter().GetResult();

        Assert.Null(result);
    }
}

public class BudgetSimulatorServiceTests
{
    [Fact]
    public void BudgetSimulationStatusClassifiesDisposableIncomeAsTightWhenResultIsPositiveButSmall()
    {
        var repo = new StubGradCastRepository();
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

        var result = service.SimulateAsync(request).GetAwaiter().GetResult();
        Assert.NotNull(result);
        Assert.Contains(result!.IncomeStatus, new[] { "tight", "deficit", "manageable", "comfortable" });
    }
}

public class FinanceEndpointValidationContractTests
{
    [Fact]
    public void FinanceEndpointValidationFailureKeepsAStableProblemShape()
    {
        var error = FinanceEndpoints.ValidateLoanRequest(-1m, 0.02m);

        Assert.NotNull(error);
        var problem = error as IResult;
        Assert.NotNull(problem);
    }
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

    public Task<decimal?> GetProgramMedianEarningsAsync(int schoolId, string cipCode, CancellationToken ct = default)
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
