using GradCast.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GradCast.Api.Endpoints;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/finance");

        group.MapGet("/net-pay", CalculateNetPay)
             .WithName("CalculateNetPay")
             .WithDescription("Calculate estimated monthly net pay after taxes");

        group.MapGet("/loan-payment", CalculateLoanPayment)
             .WithName("CalculateLoanPayment")
             .WithDescription("Calculate monthly student loan payment");
    }

    private static IResult CalculateNetPay(
        [FromQuery] decimal grossSalary,
        [FromQuery] string state,
        TaxCalculationService taxService)
    {
        if (grossSalary <= 0 || grossSalary > 10_000_000)
        {
            return Results.Problem(
                title: "Invalid salary",
                detail: "Gross salary must be between $1 and $10,000,000.",
                statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(state) || state.Length < 2)
        {
            return Results.Problem(
                title: "Invalid state",
                detail: "State must be a valid 2-letter US state code.",
                statusCode: 400);
        }

        var result = taxService.Calculate(grossSalary, state);
        return Results.Ok(result);
    }

    private static IResult CalculateLoanPayment(
        [FromQuery] decimal principal,
        [FromQuery] decimal? rate,
        [FromQuery] int? termYears,
        LoanAmortizationService loanService)
    {
        if (principal < 0 || principal > 1_000_000)
        {
            return Results.Problem(
                title: "Invalid principal",
                detail: "Loan principal must be between $0 and $1,000,000.",
                statusCode: 400);
        }

        if (rate.HasValue && (rate.Value <= 0 || rate.Value > 0.30m))
        {
            return Results.Problem(
                title: "Invalid rate",
                detail: "Annual interest rate must be between 0 and 0.30 (30%).",
                statusCode: 400);
        }

        var result = loanService.Calculate(principal, rate, termYears);
        return Results.Ok(result);
    }
}
