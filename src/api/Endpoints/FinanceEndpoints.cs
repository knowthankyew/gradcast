using GradCast.Api.Models;
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

        group.MapPost("/simulator", RunSimulation)
             .WithName("RunBudgetSimulation")
             .WithDescription("Run full budget simulation for school + location + program");
    }

    public static IResult? ValidateNetPayRequest(decimal grossSalary, string? state)
    {
        if (grossSalary <= 0 || grossSalary > 10_000_000)
        {
            return ValidationFailure(
                title: "Invalid salary",
                detail: "Gross salary must be between $1 and $10,000,000.");
        }

        if (string.IsNullOrWhiteSpace(state) || state.Length < 2)
        {
            return ValidationFailure(
                title: "Invalid state",
                detail: "State must be a valid 2-letter US state code.");
        }

        return null;
    }

    public static IResult? ValidateLoanRequest(decimal principal, decimal? rate)
    {
        if (principal < 0 || principal > 1_000_000)
        {
            return ValidationFailure(
                title: "Invalid principal",
                detail: "Loan principal must be between $0 and $1,000,000.");
        }

        if (rate.HasValue && (rate.Value <= 0 || rate.Value > 0.30m))
        {
            return ValidationFailure(
                title: "Invalid rate",
                detail: "Annual interest rate must be between 0 and 0.30 (30%).");
        }

        return null;
    }

    public static IResult? ValidateSimulationRequest(BudgetSimulationRequest request)
    {
        if (request.SchoolId <= 0)
        {
            return ValidationFailure(
                title: "Invalid school",
                detail: "A valid school ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CbsaCode))
        {
            return ValidationFailure(
                title: "Invalid location",
                detail: "A CBSA code is required for the target location.");
        }

        return null;
    }

    private static IResult CalculateNetPay(
        [FromQuery] decimal grossSalary,
        [FromQuery] string state,
        ITaxCalculationService taxService)
    {
        var invalid = ValidateNetPayRequest(grossSalary, state);
        if (invalid != null)
        {
            return invalid;
        }

        var result = taxService.Calculate(grossSalary, state);
        return Results.Ok(result);
    }

    private static IResult CalculateLoanPayment(
        [FromQuery] decimal principal,
        [FromQuery] decimal? rate,
        [FromQuery] int? termYears,
        ILoanAmortizationService loanService)
    {
        var invalid = ValidateLoanRequest(principal, rate);
        if (invalid != null)
        {
            return invalid;
        }

        var result = loanService.Calculate(principal, rate, termYears);
        return Results.Ok(result);
    }

    private static async Task<IResult> RunSimulation(
        BudgetSimulationRequest request,
        IBudgetSimulatorService simulatorService,
        CancellationToken ct)
    {
        var invalid = ValidateSimulationRequest(request);
        if (invalid != null)
        {
            return invalid;
        }

        var housingType = request.HousingType ?? "1bed";
        var normalizedRequest = request with { HousingType = housingType };

        var result = await simulatorService.SimulateAsync(normalizedRequest, ct);
        if (result == null)
        {
            return ValidationFailure(
                title: "Simulation failed",
                detail: "Could not find location data for the given CBSA code.",
                statusCode: 404);
        }

        return Results.Ok(result);
    }

    private static IResult ValidationFailure(
        string title,
        string detail,
        int statusCode = 400)
        => Results.Problem(title: title, detail: detail, statusCode: statusCode);
}
