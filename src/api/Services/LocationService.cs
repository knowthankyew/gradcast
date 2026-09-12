using Dapper;
using GradCast.Api.Models;
using GradCast.Data;
using GradCast.Data.Entities;

namespace GradCast.Api.Services;

public class LocationService : ILocationService
{
    private readonly ISqliteConnectionFactory _dbFactory;

    public LocationService(ISqliteConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<LocationSearchResult>> SearchLocationsAsync(
        string query, bool requireHousing = false, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);

        var sql = requireHousing
            ? """
              SELECT cbsa_code, name, state, type
              FROM cbsa_locations
              WHERE name LIKE @Query
                AND EXISTS (SELECT 1 FROM fair_market_rents f WHERE f.cbsa_code = cbsa_locations.cbsa_code)
              ORDER BY name ASC
              LIMIT 10;
              """
            : """
              SELECT cbsa_code, name, state, type
              FROM cbsa_locations
              WHERE name LIKE @Query
              ORDER BY name ASC
              LIMIT 10;
              """;

        var locations = (await conn.QueryAsync<CbsaLocation>(
            new CommandDefinition(sql, new { Query = $"%{query}%" }, cancellationToken: ct))).ToList();

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
}
