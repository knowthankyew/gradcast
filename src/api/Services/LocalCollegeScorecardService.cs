using System.Data.Common;
using Dapper;
using GradCast.Api.Models;
using GradCast.Data;
using GradCast.Data.Entities;

namespace GradCast.Api.Services;

/// <summary>
/// Implementation of ICollegeScorecardService that queries the local SQLite database
/// instead of the remote College Scorecard API. Use after running the import tool.
/// </summary>
public class LocalCollegeScorecardService : ICollegeScorecardService
{
    private readonly ISqliteConnectionFactory _dbFactory;
    private readonly ILogger<LocalCollegeScorecardService> _logger;

    public LocalCollegeScorecardService(ISqliteConnectionFactory dbFactory, ILogger<LocalCollegeScorecardService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SchoolSearchResult>> SearchSchoolsAsync(
        string query, string? state, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT id, name, city, state
            FROM schools
            WHERE name LIKE @Query
              AND (@State IS NULL OR state = @State)
            ORDER BY name ASC
            LIMIT 10;
        """;

        await using var reader = await conn.ExecuteReaderAsync(
            new CommandDefinition(sql, new
            {
                Query = $"%{query}%",
                State = string.IsNullOrWhiteSpace(state) ? null : state
            }, cancellationToken: ct));

        var results = new List<SchoolSearchResult>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SchoolSearchResult(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)
            ));
        }

        return results;
    }

    public async Task<bool> HasYearDataAsync(int schoolId, int year, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = "SELECT 1 FROM school_year_data WHERE school_id = @SchoolId AND year = @Year LIMIT 1;";
        var exists = await conn.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { SchoolId = schoolId, Year = year }, cancellationToken: ct));
        return exists.HasValue;
    }

    public async Task<SchoolDetail?> GetSchoolDetailAsync(
        int schoolId, int? year = null, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string schoolSql = "SELECT id, name, city, state, school_url, ownership FROM schools WHERE id = @SchoolId LIMIT 1;";
        var school = await conn.QueryFirstOrDefaultAsync<School>(
            new CommandDefinition(schoolSql, new { SchoolId = schoolId }, cancellationToken: ct));

        if (school == null) return null;

        // Get year data — use specified year or fall back to most recent available
        SchoolYearData? yearData;
        if (year.HasValue)
        {
            const string yearSql = """
                SELECT id, school_id, year, CAST(admission_rate AS REAL) as admission_rate, student_size, tuition_in_state, tuition_out_of_state, CAST(completion_rate AS REAL) as completion_rate
                FROM school_year_data
                WHERE school_id = @SchoolId AND year = @Year
                LIMIT 1;
            """;
            yearData = await conn.QueryFirstOrDefaultAsync<SchoolYearData>(
                new CommandDefinition(yearSql, new { SchoolId = schoolId, Year = year.Value }, cancellationToken: ct));
        }
        else
        {
            const string yearSql = """
                SELECT id, school_id, year, CAST(admission_rate AS REAL) as admission_rate, student_size, tuition_in_state, tuition_out_of_state, CAST(completion_rate AS REAL) as completion_rate
                FROM school_year_data
                WHERE school_id = @SchoolId
                ORDER BY year DESC
                LIMIT 1;
            """;
            yearData = await conn.QueryFirstOrDefaultAsync<SchoolYearData>(
                new CommandDefinition(yearSql, new { SchoolId = schoolId }, cancellationToken: ct));
        }

        // Get programs for the matching year (or most recent)
        var programYear = year ?? yearData?.Year ?? 2024;
        const string programsSql = """
            SELECT id, school_id, year, cip_code, title, credential_level, completions, CAST(median_earnings AS REAL) as median_earnings
            FROM programs
            WHERE school_id = @SchoolId AND year = @ProgramYear;
        """;
        var programs = await conn.QueryAsync<GradCast.Data.Entities.Program>(
            new CommandDefinition(programsSql, new { SchoolId = schoolId, ProgramYear = programYear }, cancellationToken: ct));

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

        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT year, tuition_in_state, tuition_out_of_state
            FROM school_year_data
            WHERE school_id = @SchoolId
              AND year >= @StartYear
              AND year < @CurrentYear
              AND (tuition_in_state IS NOT NULL OR tuition_out_of_state IS NOT NULL)
            ORDER BY year ASC;
        """;

        await using var reader = await conn.ExecuteReaderAsync(
            new CommandDefinition(sql, new { SchoolId = schoolId, StartYear = startYear, CurrentYear = currentYear }, cancellationToken: ct));

        var points = new List<TuitionTrendPoint>();
        while (await reader.ReadAsync(ct))
        {
            points.Add(new TuitionTrendPoint(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2)
            ));
        }

        return points;
    }
}
