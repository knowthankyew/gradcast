using System.Text;
using Dapper;
using GradCast.Api.Models;
using GradCast.Data;
using GradCast.Data.Entities;

namespace GradCast.Api.Services;

public class LocationService : ILocationService
{
    public const int MaxSearchResults = 25;

    private readonly ISqliteConnectionFactory _dbFactory;

    public LocationService(ISqliteConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<LocationSearchResult>> SearchLocationsAsync(
        string query, bool requireHousing = false, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);

        List<CbsaLocation> locations;
        if (string.IsNullOrWhiteSpace(query))
        {
            var preloadSql = requireHousing
                ? $"""
                   SELECT cbsa_code, name, state, type
                   FROM cbsa_locations
                   WHERE EXISTS (SELECT 1 FROM fair_market_rents f WHERE f.cbsa_code = cbsa_locations.cbsa_code)
                   ORDER BY name ASC
                   LIMIT {MaxSearchResults};
                   """
                : $"""
                   SELECT cbsa_code, name, state, type
                   FROM cbsa_locations
                   ORDER BY name ASC
                   LIMIT {MaxSearchResults};
                   """;

            locations = (await conn.QueryAsync<CbsaLocation>(
                new CommandDefinition(preloadSql, cancellationToken: ct))).ToList();
        }
        else
        {
            var parsed = SearchQueryParser.Parse(query);
            if (parsed.IsEmpty)
            {
                return Array.Empty<LocationSearchResult>();
            }

            var parameters = new DynamicParameters();
            var whereClauses = new List<string>();
            if (requireHousing)
            {
                whereClauses.Add("EXISTS (SELECT 1 FROM fair_market_rents f WHERE f.cbsa_code = cbsa_locations.cbsa_code)");
            }

            var paramIndex = 0;
            foreach (var term in parsed.AllSqlFilterTerms)
            {
                var paramName = $"p{paramIndex++}";
                parameters.Add(paramName, SearchQueryParser.ToUnanchoredLikePattern(term));
                whereClauses.Add(
                    $"(name LIKE @{paramName} ESCAPE '\\' OR state LIKE @{paramName} ESCAPE '\\' OR cbsa_code LIKE @{paramName} ESCAPE '\\' OR (name || ', ' || state) LIKE @{paramName} ESCAPE '\\')");
            }

            var sqlBuilder = new StringBuilder("""
                SELECT cbsa_code, name, state, type
                FROM cbsa_locations
                WHERE
                """);
            sqlBuilder.Append(' ').Append(string.Join(" AND ", whereClauses)).Append(';');

            var candidates = await conn.QueryAsync<CbsaLocation>(
                new CommandDefinition(sqlBuilder.ToString(), parameters, cancellationToken: ct));

            locations = candidates
                .Where(c => parsed.Matches(c.Name, c.State, c.CbsaCode, $"{c.Name}, {c.State}"))
                .OrderBy(c => parsed.ComputeMatchTier(c.Name, StripTrailingStateSuffix(c.Name, c.State)))
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.CbsaCode, StringComparer.Ordinal)
                .Take(MaxSearchResults)
                .ToList();
        }

        if (locations.Count == 0)
        {
            return Array.Empty<LocationSearchResult>();
        }

        var cbsaCodes = locations.Select(l => l.CbsaCode).ToList();

        const string rentsSql = """
            SELECT cbsa_code, year, one_bedroom, two_bedroom
            FROM fair_market_rents
            WHERE cbsa_code IN @CbsaCodes
            ORDER BY year DESC;
        """;

        var rents = await conn.QueryAsync<FairMarketRent>(
            new CommandDefinition(rentsSql, new { CbsaCodes = cbsaCodes }, cancellationToken: ct));

        var latestRents = rents
            .GroupBy(f => f.CbsaCode)
            .ToDictionary(g => g.Key, g => g.First());

        return locations.Select(c =>
        {
            latestRents.TryGetValue(c.CbsaCode, out var rent);
            return new LocationSearchResult(
                c.CbsaCode,
                c.Name,
                c.State,
                c.Type,
                rent?.OneBedroom,
                rent?.TwoBedroom,
                rent != null
            );
        }).ToList();
    }

    public async Task<LocationSearchResult?> GetLocationByCbsaAsync(string cbsaCode, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);

        const string sql = """
            SELECT cbsa_code, name, state, type
            FROM cbsa_locations
            WHERE cbsa_code = @CbsaCode
            LIMIT 1;
            """;

        var location = await conn.QuerySingleOrDefaultAsync<CbsaLocation>(
            new CommandDefinition(sql, new { CbsaCode = cbsaCode }, cancellationToken: ct));

        if (location == null)
        {
            return null;
        }

        const string rentSql = """
            SELECT cbsa_code, year, one_bedroom, two_bedroom
            FROM fair_market_rents
            WHERE cbsa_code = @CbsaCode
            ORDER BY year DESC
            LIMIT 1;
            """;

        var rent = await conn.QuerySingleOrDefaultAsync<FairMarketRent>(
            new CommandDefinition(rentSql, new { CbsaCode = cbsaCode }, cancellationToken: ct));

        return new LocationSearchResult(
            location.CbsaCode,
            location.Name,
            location.State,
            location.Type,
            rent?.OneBedroom,
            rent?.TwoBedroom,
            rent != null
        );
    }

    private static string? StripTrailingStateSuffix(string name, string state)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var commaIndex = name.LastIndexOf(',');
        if (commaIndex > 0)
        {
            return name[..commaIndex].Trim();
        }

        if (!string.IsNullOrWhiteSpace(state) &&
            name.EndsWith($" {state}", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^(state.Length + 1)].Trim();
        }

        return null;
    }
}
