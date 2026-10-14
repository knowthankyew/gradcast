using GradCast.Api.Models;
using GradCast.Data.Entities;

namespace GradCast.Api.Data;

public interface IScenarioRepository
{
    Task<string> InsertScenarioAsync(SharedScenario scenario, CancellationToken ct = default);
    Task<SharedScenarioDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<SharedScenarioDto>> GetPositiveFindsAsync(
        string? cbsaCode = null,
        string? cipFamily = null,
        int limit = 20,
        CancellationToken ct = default);
    Task<int> GetDailySubmissionCountByClientHashAsync(string clientHash, string datePrefixUtc, CancellationToken ct = default);
}
