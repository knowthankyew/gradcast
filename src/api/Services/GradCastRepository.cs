using GradCast.Data;
using GradCast.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Services;

public class GradCastRepository : IGradCastRepository
{
    private readonly GradCastDbContext _db;

    public GradCastRepository(GradCastDbContext db)
    {
        _db = db;
    }

    public async Task<CbsaLocation?> GetLocationByCbsaCodeAsync(string cbsaCode, CancellationToken ct = default)
    {
        return await _db.CbsaLocations
            .FirstOrDefaultAsync(c => c.CbsaCode == cbsaCode, ct);
    }

    public async Task<FairMarketRent?> GetLatestFairMarketRentAsync(string cbsaCode, CancellationToken ct = default)
    {
        return await _db.FairMarketRents
            .Where(f => f.CbsaCode == cbsaCode)
            .OrderByDescending(f => f.Year)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<decimal?> GetProgramMedianEarningsAsync(int schoolId, string cipCode, CancellationToken ct = default)
    {
        return await _db.Programs
            .Where(p => p.SchoolId == schoolId && p.CipCode.StartsWith(cipCode) && p.MedianEarnings.HasValue)
            .OrderByDescending(p => p.MedianEarnings)
            .Select(p => p.MedianEarnings)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<decimal>> GetSchoolMedianEarningsAsync(int schoolId, CancellationToken ct = default)
    {
        return await _db.Programs
            .Where(p => p.SchoolId == schoolId && p.MedianEarnings.HasValue)
            .Select(p => p.MedianEarnings!.Value)
            .ToListAsync(ct);
    }

    public async Task<decimal> GetMedianDebtAsync(int schoolId, CancellationToken ct = default)
    {
        var yearData = await _db.SchoolYearData
            .Where(yd => yd.SchoolId == schoolId)
            .OrderByDescending(yd => yd.Year)
            .FirstOrDefaultAsync(ct);

        if (yearData?.TuitionInState == null) return 28000m;

        var estimatedDebt = yearData.TuitionInState.Value * 4 * 0.6m;
        return Math.Min(estimatedDebt, 150_000m);
    }
}
