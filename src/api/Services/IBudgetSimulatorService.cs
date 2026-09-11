using GradCast.Api.Models;

namespace GradCast.Api.Services;

public interface IBudgetSimulatorService
{
    Task<BudgetSimulationResult?> SimulateAsync(BudgetSimulationRequest request, CancellationToken ct = default);
}
