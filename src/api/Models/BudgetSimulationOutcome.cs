namespace GradCast.Api.Models;

/// <summary>
/// The result of attempting a budget simulation. A successful outcome preserves the
/// established <see cref="BudgetSimulationResult"/> response contract; unavailable
/// reference data is represented explicitly instead of being converted to a zero cost.
/// </summary>
public abstract record BudgetSimulationOutcome;

public sealed record BudgetSimulationSuccess(BudgetSimulationResult Simulation) : BudgetSimulationOutcome;

public enum BudgetSimulationUnavailableReason
{
    LocationNotFound,
    HousingDataUnavailable
}

public sealed record BudgetSimulationUnavailable(
    BudgetSimulationUnavailableReason Reason,
    string Detail) : BudgetSimulationOutcome;
