using GradCast.Api.Configuration;
using GradCast.Api.Models;
using GradCast.Api.Services;
using GradCast.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GradCast.Api.Tests;

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
    {
        if (request.CbsaCode == "99999")
        {
            return Task.FromResult<BudgetSimulationOutcome>(new BudgetSimulationUnavailable(
                BudgetSimulationUnavailableReason.LocationNotFound,
                "No location found for CBSA code '99999'."));
        }

        return Task.FromResult<BudgetSimulationOutcome>(new BudgetSimulationUnavailable(
            BudgetSimulationUnavailableReason.HousingDataUnavailable,
            "No Fair Market Rent data is available for CBSA code '12345'."));
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
