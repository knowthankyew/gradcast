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
        string query, CancellationToken ct = default)
    {
        return await _db.CbsaLocations
            .AsNoTracking()
            .Where(c => EF.Functions.Like(c.Name, $"%{query}%"))
            .OrderBy(c => c.Name)
            .Take(10)
            .Select(c => new LocationSearchResult(c.CbsaCode, c.Name, c.State, c.Type))
            .ToListAsync(ct);
    }
}
