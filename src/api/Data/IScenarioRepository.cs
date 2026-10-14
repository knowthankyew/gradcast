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
    /// <summary>
    /// Diagnostic helper to inspect daily submissions for an anonymized client hash.
    /// Note: Do not use for concurrency-sensitive rate-limiting enforcement; use <see cref="TryInsertScenarioWithDailyLimitAsync"/>.
    /// </summary>
    Task<int> GetDailySubmissionCountByClientHashAsync(string clientHash, string datePrefixUtc, CancellationToken ct = default);

    /// <summary>
    /// Atomically verifies client daily limit and persists scenario within an atomic statement to prevent TOCTOU concurrency races.
    /// </summary>
    Task<bool> TryInsertScenarioWithDailyLimitAsync(SharedScenario scenario, int maxDailyLimit, string datePrefixUtc, CancellationToken ct = default);
}
