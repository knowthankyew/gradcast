using GradCast.Api.Models;
using GradCast.Api.Services;
using GradCast.Data.Entities;

namespace GradCast.Api.Tests;

public class BudgetSimulatorServiceTests
{
    [Fact]
    public async Task BudgetSimulationCalculatesDeterministicBreakdownAndStatus()
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

        // Low salary scenario -> Deficit
        var deficitRequest = new BudgetSimulationRequest(1, string.Empty, "12345", "1bed", 1000m);
        var deficitResult = await service.SimulateAsync(deficitRequest);
        var deficitSuccess = Assert.IsType<BudgetSimulationSuccess>(deficitResult);
        var sim = deficitSuccess.Simulation;
        Assert.Equal("deficit", sim.IncomeStatus);
        Assert.Equal(1000m, sim.GrossAnnualSalary);
        Assert.Equal(83.33m, sim.GrossMonthly);
        Assert.Equal(76.96m, sim.NetMonthly);
        Assert.Equal(900m, sim.RentMonthly);
        Assert.Equal(108.53m, sim.LoanPaymentMonthly);
        Assert.Equal(1008.53m, sim.FixedCostsMonthly);
        Assert.Equal(-931.57m, sim.DisposableMonthly);

        // High salary scenario -> Comfortable
        var comfortableRequest = new BudgetSimulationRequest(1, string.Empty, "12345", "1bed", 60000m);
        var comfortableResult = await service.SimulateAsync(comfortableRequest);
        var comfortableSuccess = Assert.IsType<BudgetSimulationSuccess>(comfortableResult);
        var comfSim = comfortableSuccess.Simulation;
        Assert.Equal("comfortable", comfSim.IncomeStatus);
        Assert.Equal(60000m, comfSim.GrossAnnualSalary);
        Assert.Equal(5000m, comfSim.GrossMonthly);
        Assert.Equal(4617.50m, comfSim.NetMonthly);
        Assert.Equal(900m, comfSim.RentMonthly);
        Assert.Equal(108.53m, comfSim.LoanPaymentMonthly);
        Assert.Equal(1008.53m, comfSim.FixedCostsMonthly);
        Assert.Equal(3608.97m, comfSim.DisposableMonthly);
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
