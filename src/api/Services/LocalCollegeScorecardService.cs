using GradCast.Api.Models;
using GradCast.Data;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Services;

/// <summary>
/// Implementation of ICollegeScorecardService that queries the local SQLite database
/// instead of the remote College Scorecard API. Use after running the import tool.
/// </summary>
public class LocalCollegeScorecardService : ICollegeScorecardService
{
    private readonly GradCastDbContext _db;
    private readonly ILogger<LocalCollegeScorecardService> _logger;


    public LocalCollegeScorecardService(GradCastDbContext db, ILogger<LocalCollegeScorecardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SchoolSearchResult>> SearchSchoolsAsync(
        string query, string? state, CancellationToken ct = default)
    {
        var q = _db.Schools.AsNoTracking().AsQueryable();

        q = q.Where(s => EF.Functions.Like(s.Name, $"%{query}%"));

        if (!string.IsNullOrWhiteSpace(state))
        {
            q = q.Where(s => s.State == state);
        }

        var results = await q
            .OrderBy(s => s.Name)
            .Take(10)
            .Select(s => new SchoolSearchResult(s.Id, s.Name, s.City, s.State))
            .ToListAsync(ct);

        return results;
    }

    public async Task<SchoolDetail?> GetSchoolDetailAsync(
        int schoolId, int? year = null, CancellationToken ct = default)
    {
        var school = await _db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null) return null;

        // Get year data — use specified year or fall back to most recent available
        var yearDataQuery = _db.SchoolYearData.AsNoTracking().Where(yd => yd.SchoolId == schoolId);
        Data.Entities.SchoolYearData? yearData;

        if (year.HasValue)
        {
            yearData = await yearDataQuery.FirstOrDefaultAsync(yd => yd.Year == year.Value, ct);
        }
        else
        {
            yearData = await yearDataQuery.OrderByDescending(yd => yd.Year).FirstOrDefaultAsync(ct);
        }

        // Get programs for the matching year (or most recent)
        var programYear = year ?? yearData?.Year ?? 2024;
        var programs = await _db.Programs
            .AsNoTracking()
            .Where(p => p.SchoolId == schoolId && p.Year == programYear)
            .ToListAsync(ct);

        var programDtos = programs.Select(p => new ProgramData(
            Code: p.CipCode.Replace(".", ""), // Normalize to 4-digit without dot
            Title: p.Title,
            CredentialLevel: p.CredentialLevel,
            CredentialName: ScorecardLookups.CredentialLevels.GetValueOrDefault(p.CredentialLevel, "Unknown"),
            Completions: p.Completions,
            MedianEarnings: p.MedianEarnings
        )).ToList();

        return new SchoolDetail(
            Id: school.Id,
            Name: school.Name,
            City: school.City,
            State: school.State,
            SchoolUrl: school.SchoolUrl,
            Ownership: school.Ownership,
            OwnershipName: ScorecardLookups.OwnershipTypes.GetValueOrDefault(school.Ownership, "Unknown"),
            AdmissionRate: yearData?.AdmissionRate,
            StudentSize: yearData?.StudentSize,
            TuitionInState: yearData?.TuitionInState,
            TuitionOutOfState: yearData?.TuitionOutOfState,
            CompletionRate: yearData?.CompletionRate,
            Programs: programDtos
        );
    }

    public async Task<IReadOnlyList<TuitionTrendPoint>> GetTuitionTrendAsync(
        int schoolId, CancellationToken ct = default)
    {
        var currentYear = DateTime.UtcNow.Year;
        var startYear = currentYear - 5;

        var yearData = await _db.SchoolYearData
            .AsNoTracking()
            .Where(yd => yd.SchoolId == schoolId && yd.Year >= startYear && yd.Year < currentYear)
            .OrderBy(yd => yd.Year)
            .ToListAsync(ct);

        return yearData
            .Where(yd => yd.TuitionInState.HasValue || yd.TuitionOutOfState.HasValue)
            .Select(yd => new TuitionTrendPoint(yd.Year, yd.TuitionInState, yd.TuitionOutOfState))
            .ToList();
    }
}
