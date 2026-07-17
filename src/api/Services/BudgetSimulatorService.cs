using GradCast.Api.Models;
using GradCast.Data;
using Microsoft.EntityFrameworkCore;

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
public class BudgetSimulatorService
{
    private readonly GradCastDbContext _db;
    private readonly HousingCostService _housingService;
    private readonly TaxCalculationService _taxService;
    private readonly LoanAmortizationService _loanService;

    public BudgetSimulatorService(
        GradCastDbContext db,
        HousingCostService housingService,
        TaxCalculationService taxService,
        LoanAmortizationService loanService)
    {
        _db = db;
        _housingService = housingService;
        _taxService = taxService;
        _loanService = loanService;
    }

    public async Task<BudgetSimulationResult?> SimulateAsync(
        BudgetSimulationRequest request, CancellationToken ct = default)
    {
        // 1. Determine salary (user override > program median earnings > school-wide median)
        var salary = await ResolveSalaryAsync(request, ct);
        var salarySource = request.SalaryOverride.HasValue ? "user_override" : "scorecard_median";

        if (salary <= 0)
        {
            salary = 45000m; // National median starting salary fallback
            salarySource = "national_fallback";
        }

        // 2. Get location info for state tax lookup
        var location = await _db.CbsaLocations
            .FirstOrDefaultAsync(c => c.CbsaCode == request.CbsaCode, ct);

        if (location == null) return null;

        // 3. Calculate net pay
        var netPay = _taxService.Calculate(salary, location.State);

        // 4. Get housing cost
        var housing = await _housingService.GetHousingCostAsync(
            request.CbsaCode, request.HousingType, ct);

        var rentMonthly = housing?.MonthlyRent ?? 0;

        // 5. Calculate loan payment from school's median debt
        var medianDebt = await GetMedianDebtAsync(request.SchoolId, ct);
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

        return new BudgetSimulationResult(
            GrossAnnualSalary: salary,
            GrossMonthly: netPay.GrossMonthly,
            NetMonthly: netPay.NetMonthly,
            EffectiveTaxRate: netPay.EffectiveTaxRate,
            SalarySource: salarySource,
            RentMonthly: rentMonthly,
            HousingType: request.HousingType,
            LoanPaymentMonthly: loan.MonthlyPayment,
            LoanPrincipal: loan.Principal,
            FixedCostsMonthly: Math.Round(fixedCosts, 2),
            DisposableMonthly: Math.Round(disposable, 2),
            IncomeStatus: status,
            LocationName: location.Name,
            State: location.State
        );
    }

    private async Task<decimal> ResolveSalaryAsync(BudgetSimulationRequest request, CancellationToken ct)
    {
        if (request.SalaryOverride.HasValue && request.SalaryOverride.Value > 0)
        {
            return request.SalaryOverride.Value;
        }

        // Try program-specific median earnings
        if (!string.IsNullOrEmpty(request.CipCode))
        {
            var programEarnings = await _db.Programs
                .Where(p => p.SchoolId == request.SchoolId &&
                            p.CipCode.StartsWith(request.CipCode) &&
                            p.MedianEarnings.HasValue)
                .OrderByDescending(p => p.MedianEarnings)
                .Select(p => p.MedianEarnings)
                .FirstOrDefaultAsync(ct);

            if (programEarnings.HasValue && programEarnings.Value > 0)
            {
                return programEarnings.Value;
            }
        }

        // Fall back to school-wide median across all programs
        var earningsList = await _db.Programs
            .Where(p => p.SchoolId == request.SchoolId && p.MedianEarnings.HasValue)
            .Select(p => p.MedianEarnings!.Value)
            .ToListAsync(ct);

        if (earningsList.Count == 0) return 0;

        var schoolMedian = earningsList.Average();
        return schoolMedian > 0 ? Math.Round(schoolMedian, 0) : 0;
    }

    private async Task<decimal> GetMedianDebtAsync(int schoolId, CancellationToken ct)
    {
        // For the PoC, estimate debt from tuition data
        // In a full implementation, we'd use the Scorecard's median_debt_suppressed field
        var yearData = await _db.SchoolYearData
            .Where(yd => yd.SchoolId == schoolId)
            .OrderByDescending(yd => yd.Year)
            .FirstOrDefaultAsync(ct);

        if (yearData?.TuitionInState == null) return 28000m; // National average fallback

        // Estimate: ~60% of 4-year tuition as borrowed (accounts for aid/scholarships)
        var estimatedDebt = yearData.TuitionInState.Value * 4 * 0.6m;

        // Cap at a reasonable maximum
        return Math.Min(estimatedDebt, 150_000m);
    }
}
