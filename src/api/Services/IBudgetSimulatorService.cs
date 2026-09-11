using GradCast.Api.Models;

namespace GradCast.Api.Services;

public interface IBudgetSimulatorService
{
    Task<BudgetSimulationOutcome> SimulateAsync(BudgetSimulationRequest request, CancellationToken ct = default);
}
