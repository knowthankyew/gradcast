using GradCast.Api.Models;

namespace GradCast.Api.Services;

public interface ILoanAmortizationService
{
    LoanPaymentResult Calculate(decimal principalBalance, decimal? annualRate = null, int? termYears = null);
}
