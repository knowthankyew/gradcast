using Dapper;
using GradCast.Data;
using GradCast.Data.Entities;

namespace GradCast.Api.Services;

public class GradCastRepository : IGradCastRepository
{
    private readonly ISqliteConnectionFactory _dbFactory;

    public GradCastRepository(ISqliteConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<CbsaLocation?> GetLocationByCbsaCodeAsync(string cbsaCode, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = "SELECT cbsa_code, name, state, type FROM cbsa_locations WHERE cbsa_code = @CbsaCode LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<CbsaLocation>(
            new CommandDefinition(sql, new { CbsaCode = cbsaCode }, cancellationToken: ct));
    }

    public async Task<FairMarketRent?> GetLatestFairMarketRentAsync(string cbsaCode, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT id, cbsa_code, year, efficiency, one_bedroom, two_bedroom, three_bedroom, four_bedroom
            FROM fair_market_rents
            WHERE cbsa_code = @CbsaCode
            ORDER BY year DESC
            LIMIT 1;
        """;
        return await conn.QueryFirstOrDefaultAsync<FairMarketRent>(
            new CommandDefinition(sql, new { CbsaCode = cbsaCode }, cancellationToken: ct));
    }

    public async Task<decimal?> GetProgramMedianEarningsAsync(
        int schoolId,
        string cipCode,
        int? credentialLevel = null,
        CancellationToken ct = default)
    {
        var normalizedCipCode = CipCode.NormalizeToFourDigit(cipCode);
        if (normalizedCipCode is null) return null;

        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT CAST(median_earnings AS REAL)
            FROM programs
            WHERE school_id = @SchoolId
              AND REPLACE(cip_code, '.', '') = @NormalizedCipCode
              AND median_earnings IS NOT NULL
              AND (@CredentialLevel IS NULL OR credential_level = @CredentialLevel)
            ORDER BY CAST(median_earnings AS REAL) ASC;
        """;

        var earnings = (await conn.QueryAsync<decimal>(
            new CommandDefinition(sql, new
            {
                SchoolId = schoolId,
                NormalizedCipCode = normalizedCipCode,
                CredentialLevel = credentialLevel
            }, cancellationToken: ct))).ToList();

        if (earnings.Count == 0) return null;

        // A selected program supplies its credential level and resolves to its exact row.
        // Older saved scenarios do not have that field, so use the statistical median of
        // matching credentials rather than biasing the simulation toward the highest salary.
        var middle = earnings.Count / 2;
        return earnings.Count % 2 == 1
            ? earnings[middle]
            : (earnings[middle - 1] + earnings[middle]) / 2;
    }

    public async Task<List<decimal>> GetSchoolMedianEarningsAsync(int schoolId, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT CAST(median_earnings AS REAL)
            FROM programs
            WHERE school_id = @SchoolId AND median_earnings IS NOT NULL;
        """;

        var earnings = await conn.QueryAsync<decimal>(
            new CommandDefinition(sql, new { SchoolId = schoolId }, cancellationToken: ct));
        return earnings.ToList();
    }

    public async Task<decimal> GetMedianDebtAsync(int schoolId, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT tuition_in_state
            FROM school_year_data
            WHERE school_id = @SchoolId
            ORDER BY year DESC
            LIMIT 1;
        """;

        var tuitionInState = await conn.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(sql, new { SchoolId = schoolId }, cancellationToken: ct));

        if (tuitionInState == null) return 28000m;

        var estimatedDebt = tuitionInState.Value * 4 * 0.6m;
        return Math.Min(estimatedDebt, 150_000m);
    }
}
