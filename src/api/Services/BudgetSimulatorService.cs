using GradCast.Api.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace GradCast.Api.Services;

/// <summary>
/// Orchestrates budget simulation using only local/fast data sources:
/// - Salary from College Scorecard median earnings (or user override)
/// - Housing from HUD FMR
/// - Loan payment from Scorecard median debt + amortization math
/// - Taxes from local bracket calculations
///
/// Does NOT call external APIs (Adzuna, etc.) — those are async on the frontend.
/// </summary>
public class BudgetSimulatorService : IBudgetSimulatorService
{
    private readonly IGradCastRepository _repo;
    private readonly IHousingCostService _housingService;
    private readonly ITaxCalculationService _taxService;
    private readonly ILoanAmortizationService _loanService;
    private readonly ILogger<BudgetSimulatorService> _logger;

    public BudgetSimulatorService(
        IGradCastRepository repo,
        IHousingCostService housingService,
        ITaxCalculationService taxService,
        ILoanAmortizationService loanService,
        ILogger<BudgetSimulatorService>? logger = null)
    {
        _repo = repo;
        _housingService = housingService;
        _taxService = taxService;
        _loanService = loanService;
        _logger = logger ?? NullLogger<BudgetSimulatorService>.Instance;
    }

    public async Task<BudgetSimulationOutcome> SimulateAsync(
        BudgetSimulationRequest request, CancellationToken ct = default)
    {
        // 1. Determine salary (user override > program median earnings > school-wide median > national fallback)
        var resolved = await ResolveSalaryAsync(request, ct);
        var salary = resolved.Amount;
        var salarySource = resolved.Source;
        var hasReportedEarnings = resolved.HasReportedEarnings;

        // 2. Get location info for state tax lookup
        var location = await _repo.GetLocationByCbsaCodeAsync(request.CbsaCode, ct);

        if (location == null)
        {
            return new BudgetSimulationUnavailable(
                BudgetSimulationUnavailableReason.LocationNotFound,
                $"Could not find location data for CBSA code '{request.CbsaCode}'.");
        }

        // 3. Calculate net pay
        var netPay = _taxService.Calculate(salary, location.State);

        // 4. Get housing cost
        var housing = await _housingService.GetHousingCostAsync(
            request.CbsaCode, request.HousingType, ct);

        if (housing == null)
        {
            return new BudgetSimulationUnavailable(
                BudgetSimulationUnavailableReason.HousingDataUnavailable,
                $"No Fair Market Rent data is available for CBSA code '{request.CbsaCode}'.");
        }

        var rentMonthly = housing.MonthlyRent;

        // 5. Calculate loan payment from school's median debt
        var medianDebt = await _repo.GetMedianDebtAsync(request.SchoolId, ct);
        var loan = _loanService.Calculate(medianDebt);

        // 6. Assemble the budget
        var fixedCosts = rentMonthly + loan.MonthlyPayment;
        var disposable = netPay.NetMonthly - fixedCosts;

        var status = disposable switch
        {
            > 1500m => "comfortable",
            > 500m => "manageable",
            > 0m => "tight",
            _ => "deficit"
        };

        _logger.LogInformation(
            "Calculated budget simulation: School={SchoolId}, Metro={CbsaCode}, Gross={GrossAnnual}, NetMonthly={NetMonthly}, Disposable={DisposableMonthly}, Status={IncomeStatus}",
            request.SchoolId, request.CbsaCode, salary, netPay.NetMonthly, Math.Round(disposable, 2), status);

        return new BudgetSimulationSuccess(new BudgetSimulationResult(
            GrossAnnualSalary: salary,
            GrossMonthly: netPay.GrossMonthly,
            NetMonthly: netPay.NetMonthly,
            EffectiveTaxRate: netPay.EffectiveTaxRate,
            SalarySource: salarySource,
            HasReportedEarnings: hasReportedEarnings,
            RentMonthly: rentMonthly,
            HousingType: request.HousingType,
            LoanPaymentMonthly: loan.MonthlyPayment,
            LoanPrincipal: loan.Principal,
            FixedCostsMonthly: Math.Round(fixedCosts, 2),
            DisposableMonthly: Math.Round(disposable, 2),
            IncomeStatus: status,
            LocationName: location.Name,
            State: location.State
        ));
    }

    private record ResolvedSalary(decimal Amount, string Source, bool HasReportedEarnings);

    private async Task<ResolvedSalary> ResolveSalaryAsync(BudgetSimulationRequest request, CancellationToken ct)
    {
        if (request.SalaryOverride.HasValue && request.SalaryOverride.Value > 0)
        {
            return new ResolvedSalary(request.SalaryOverride.Value, "user_override", true);
        }

        // Try program-specific median earnings
        if (!string.IsNullOrEmpty(request.CipCode))
        {
            var programEarnings = await _repo.GetProgramMedianEarningsAsync(
                request.SchoolId,
                request.CipCode,
                request.CredentialLevel,
                ct);
            if (programEarnings.HasValue && programEarnings.Value > 0)
            {
                return new ResolvedSalary(programEarnings.Value, "program_median", true);
            }

            // Selected major has no reported earnings in College Scorecard
            return new ResolvedSalary(45000m, "national_fallback", false);
        }

        // School-wide average request
        var earningsList = await _repo.GetSchoolMedianEarningsAsync(request.SchoolId, ct);
        if (earningsList.Count > 0)
        {
            var schoolMedian = earningsList.Average();
            if (schoolMedian > 0)
            {
                return new ResolvedSalary(Math.Round(schoolMedian, 0), "school_median", true);
            }
        }

        // School has no reported earnings for any program
        return new ResolvedSalary(45000m, "national_fallback", false);
    }
}
