using GradCast.Api.Models;
using GradCast.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GradCast.Api.Endpoints;

public static class FinanceEndpoints
{
    private const decimal MaxSalary = 10_000_000m;
    private const int MinLoanTermYears = 1;
    private const int MaxLoanTermYears = 50;
    private static readonly HashSet<string> SupportedHousingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "studio", "1bed", "2bed"
    };

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
        if (request.GrossSalary <= 0 || request.GrossSalary > MaxSalary)
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

        if (request.Rate.HasValue && (request.Rate.Value < 0 || request.Rate.Value > 0.30m))
        {
            return ValidationFailure(
                title: "Invalid rate",
                detail: "Annual interest rate must be between 0 and 0.30 (30%).");
        }

        if (request.TermYears.HasValue &&
            (request.TermYears.Value < MinLoanTermYears || request.TermYears.Value > MaxLoanTermYears))
        {
            return ValidationFailure(
                title: "Invalid loan term",
                detail: $"Loan term must be between {MinLoanTermYears} and {MaxLoanTermYears} years.");
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

        if (!SupportedHousingTypes.Contains(request.HousingType))
        {
            return ValidationFailure(
                title: "Invalid housing type",
                detail: "Housing type must be 'studio', '1bed', or '2bed'.");
        }

        if (request.SalaryOverride.HasValue &&
            (request.SalaryOverride.Value <= 0 || request.SalaryOverride.Value > MaxSalary))
        {
            return ValidationFailure(
                title: "Invalid salary override",
                detail: "Salary override must be between $1 and $10,000,000.");
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
        // Preserve the original default for clients that omit housingType, while rejecting
        // unsupported values before the simulation or its dependencies are invoked.
        var housingType = request.HousingType ?? "1bed";
        var normalizedRequest = request with { HousingType = housingType.ToLowerInvariant() };

        var invalid = ValidateSimulationRequest(normalizedRequest);
        if (invalid != null)
        {
            return invalid;
        }

        var outcome = await simulatorService.SimulateAsync(normalizedRequest, ct);

        return outcome switch
        {
            BudgetSimulationSuccess success => Results.Ok(success.Simulation),
            BudgetSimulationUnavailable { Reason: BudgetSimulationUnavailableReason.LocationNotFound } unavailable
                => ValidationFailure("Location not found", unavailable.Detail, statusCode: 404),
            BudgetSimulationUnavailable { Reason: BudgetSimulationUnavailableReason.HousingDataUnavailable } unavailable
                => ValidationFailure("Housing data unavailable", unavailable.Detail, statusCode: 422),
            _ => Results.Problem(
                title: "Simulation failed",
                detail: "The budget simulation returned an unrecognized result.",
                statusCode: 500)
        };
    }

    private static IResult ValidationFailure(
        string title,
        string detail,
        int statusCode = 400)
        => Results.Problem(title: title, detail: detail, statusCode: statusCode);
}
