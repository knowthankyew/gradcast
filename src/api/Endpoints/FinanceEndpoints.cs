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

    public static IResult? ValidateNetPayRequest(NetPayRequest request)
    {
        if (request.GrossSalary <= 0 || request.GrossSalary > 10_000_000)
        {
            return ValidationFailure(
                title: "Invalid salary",
                detail: "Gross salary must be between $1 and $10,000,000.");
        }

        if (string.IsNullOrWhiteSpace(request.State) || request.State.Length < 2)
        {
            return ValidationFailure(
                title: "Invalid state",
                detail: "State must be a valid 2-letter US state code.");
        }

        return null;
    }

    public static IResult? ValidateLoanRequest(LoanPaymentRequest request)
    {
        if (request.Principal < 0 || request.Principal > 1_000_000)
        {
            return ValidationFailure(
                title: "Invalid principal",
                detail: "Loan principal must be between $0 and $1,000,000.");
        }

        if (request.Rate.HasValue && (request.Rate.Value <= 0 || request.Rate.Value > 0.30m))
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
        var request = new NetPayRequest(grossSalary, state);
        var invalid = ValidateNetPayRequest(request);
        if (invalid != null)
        {
            return invalid;
        }

        var result = taxService.Calculate(request.GrossSalary, request.State);
        return Results.Ok(result);
    }

    private static IResult CalculateLoanPayment(
        [FromQuery] decimal principal,
        [FromQuery] decimal? rate,
        [FromQuery] int? termYears,
        ILoanAmortizationService loanService)
    {
        var request = new LoanPaymentRequest(principal, rate, termYears);
        var invalid = ValidateLoanRequest(request);
        if (invalid != null)
        {
            return invalid;
        }

        var result = loanService.Calculate(request.Principal, request.Rate, request.TermYears);
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
