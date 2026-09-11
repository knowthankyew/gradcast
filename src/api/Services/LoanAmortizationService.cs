using GradCast.Api.Models;

namespace GradCast.Api.Services;

/// <summary>
/// Calculates standard federal student loan monthly payments.
/// Uses standard amortization formula with configurable rate and term.
/// </summary>
public class LoanAmortizationService : ILoanAmortizationService
{
    private const decimal DefaultAnnualRate = 0.055m;  // 5.5% federal direct loan rate
    private const int DefaultTermYears = 10;            // Standard repayment plan

    public LoanPaymentResult Calculate(
        decimal principalBalance,
        decimal? annualRate = null,
        int? termYears = null)
    {
        var rate = annualRate ?? DefaultAnnualRate;
        var years = termYears ?? DefaultTermYears;

        if (principalBalance <= 0)
        {
            return new LoanPaymentResult(0, 0, 0, 0, rate, years);
        }

        var monthlyRate = rate / 12;
        var totalPayments = years * 12;

        // Standard amortization: P * [r(1+r)^n] / [(1+r)^n - 1]
        decimal monthlyPayment;
        if (monthlyRate == 0)
        {
            monthlyPayment = principalBalance / totalPayments;
        }
        else
        {
            var rateMultiplier = (double)monthlyRate;
            var factor = Math.Pow(1 + rateMultiplier, totalPayments);
            monthlyPayment = principalBalance * (decimal)(rateMultiplier * factor / (factor - 1));
        }

        var totalPaid = monthlyPayment * totalPayments;
        var totalInterest = totalPaid - principalBalance;

        return new LoanPaymentResult(
            Principal: Math.Round(principalBalance, 2),
            MonthlyPayment: Math.Round(monthlyPayment, 2),
            TotalPaid: Math.Round(totalPaid, 2),
            TotalInterest: Math.Round(totalInterest, 2),
            AnnualRate: rate,
            TermYears: years
        );
    }
}
