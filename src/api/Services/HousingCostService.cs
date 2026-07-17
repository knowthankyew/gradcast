using GradCast.Api.Models;
using GradCast.Data;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Services;

public class HousingCostService
{
    private readonly GradCastDbContext _db;

    public HousingCostService(GradCastDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns monthly rent for the given CBSA and housing type.
    /// </summary>
    public async Task<HousingCostResult?> GetHousingCostAsync(
        string cbsaCode, string housingType, CancellationToken ct = default)
    {
        var fmr = await _db.FairMarketRents
            .Where(f => f.CbsaCode == cbsaCode)
            .OrderByDescending(f => f.Year)
            .FirstOrDefaultAsync(ct);

        if (fmr == null) return null;

        var monthlyRent = housingType switch
        {
            "1bed" => fmr.OneBedroom,
            "2bed" => fmr.TwoBedroom / 2, // Split with roommate
            "studio" => fmr.Efficiency,
            _ => fmr.OneBedroom
        };

        return new HousingCostResult(
            CbsaCode: cbsaCode,
            HousingType: housingType,
            MonthlyRent: monthlyRent,
            FmrYear: fmr.Year,
            FullRent: housingType == "2bed" ? fmr.TwoBedroom : (housingType == "studio" ? fmr.Efficiency : fmr.OneBedroom)
        );
    }
}
