namespace GradCast.Api.Models;

public record LoanPaymentResult(
    decimal Principal,
    decimal MonthlyPayment,
    decimal TotalPaid,
    decimal TotalInterest,
    decimal AnnualRate,
    int TermYears
);
