using GradCast.Api.Models;
using GradCast.Data;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Services;

public class LocationService
{
    private readonly GradCastDbContext _db;

    public LocationService(GradCastDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LocationSearchResult>> SearchLocationsAsync(
        string query, bool requireHousing = false, CancellationToken ct = default)
    {
        var locationsQuery = _db.CbsaLocations
            .AsNoTracking()
            .Where(c => EF.Functions.Like(c.Name, $"%{query}%"));

        if (requireHousing)
        {
            locationsQuery = locationsQuery.Where(c =>
                _db.FairMarketRents.Any(f => f.CbsaCode == c.CbsaCode));
        }

        var locations = await locationsQuery
            .OrderBy(c => c.Name)
            .Take(10)
            .ToListAsync(ct);

        if (locations.Count == 0)
        {
            return Array.Empty<LocationSearchResult>();
        }

        var cbsaCodes = locations.Select(l => l.CbsaCode).ToList();

        var rents = await _db.FairMarketRents
            .AsNoTracking()
            .Where(f => cbsaCodes.Contains(f.CbsaCode))
            .OrderByDescending(f => f.Year)
            .ToListAsync(ct);

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
