using GradCast.Api.Models;
using GradCast.Data;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Services;

/// <summary>
/// Hybrid implementation: queries local SQLite first, falls back to the remote
/// College Scorecard API for data not present locally (e.g., historic years).
/// </summary>
public class HybridCollegeScorecardService : ICollegeScorecardService
{
    private readonly LocalCollegeScorecardService _local;
    private readonly CollegeScorecardService _remote;
    private readonly GradCastDbContext _db;
    private readonly ILogger<HybridCollegeScorecardService> _logger;

    public HybridCollegeScorecardService(
        LocalCollegeScorecardService local,
        CollegeScorecardService remote,
        GradCastDbContext db,
        ILogger<HybridCollegeScorecardService> logger)
    {
        _local = local;
        _remote = remote;
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SchoolSearchResult>> SearchSchoolsAsync(
        string query, string? state, CancellationToken ct = default)
    {
        // Search is always fast locally — use local DB
        var results = await _local.SearchSchoolsAsync(query, state, ct);

        if (results.Count > 0)
        {
            _logger.LogDebug("Search served from local DB: {Count} results", results.Count);
            return results;
        }

        // If local returns nothing, try remote (school might not be in the import)
        _logger.LogDebug("Search falling back to remote API for query: {Query}", query);
        return await _remote.SearchSchoolsAsync(query, state, ct);
    }

    public async Task<SchoolDetail?> GetSchoolDetailAsync(
        int schoolId, int? year = null, CancellationToken ct = default)
    {
        if (year == null)
        {
            // No specific year → try local first (has "latest" imported data)
            var localResult = await _local.GetSchoolDetailAsync(schoolId, null, ct);
            if (localResult != null && localResult.Programs.Count > 0)
            {
                _logger.LogDebug("Detail for school {Id} served from local DB", schoolId);
                return localResult;
            }
        }
        else
        {
            // Specific year requested — check if we have year data locally
            var hasLocalYear = await _db.SchoolYearData
                .AnyAsync(yd => yd.SchoolId == schoolId && yd.Year == year.Value, ct);

            if (hasLocalYear)
            {
                var localResult = await _local.GetSchoolDetailAsync(schoolId, year, ct);
                if (localResult != null)
                {
                    _logger.LogDebug("Detail for school {Id} year {Year} served from local DB", schoolId, year);
                    return localResult;
                }
            }
        }

        // Fall back to remote API
        _logger.LogDebug("Detail for school {Id} year {Year} falling back to remote API", schoolId, year);
        return await _remote.GetSchoolDetailAsync(schoolId, year, ct);
    }

    public async Task<IReadOnlyList<TuitionTrendPoint>> GetTuitionTrendAsync(
        int schoolId, CancellationToken ct = default)
    {
        // Try local first — will have data if multiple years were imported
        var localTrend = await _local.GetTuitionTrendAsync(schoolId, ct);

        if (localTrend.Count >= 3)
        {
            _logger.LogDebug("Tuition trend for school {Id} served from local DB ({Count} points)", schoolId, localTrend.Count);
            return localTrend;
        }

        // Not enough local data for a meaningful trend — use remote API
        _logger.LogDebug("Tuition trend for school {Id} falling back to remote API", schoolId);
        return await _remote.GetTuitionTrendAsync(schoolId, ct);
    }
}
