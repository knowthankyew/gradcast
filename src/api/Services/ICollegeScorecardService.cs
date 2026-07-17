using GradCast.Api.Models;

namespace GradCast.Api.Services;

public interface ICollegeScorecardService
{
    Task<IReadOnlyList<SchoolSearchResult>> SearchSchoolsAsync(string query, string? state, CancellationToken ct = default);
    Task<SchoolDetail?> GetSchoolDetailAsync(int schoolId, int? year = null, CancellationToken ct = default);
    Task<IReadOnlyList<TuitionTrendPoint>> GetTuitionTrendAsync(int schoolId, CancellationToken ct = default);
}
