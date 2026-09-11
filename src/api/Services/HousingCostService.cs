using GradCast.Api.Models;

namespace GradCast.Api.Services;

public class HousingCostService
{
    private readonly IGradCastRepository _repo;

    public HousingCostService(IGradCastRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// Returns monthly rent for the given CBSA and housing type.
    /// </summary>
    public async Task<HousingCostResult?> GetHousingCostAsync(
        string cbsaCode, string housingType, CancellationToken ct = default)
    {
        var fmr = await _repo.GetLatestFairMarketRentAsync(cbsaCode, ct);

        if (fmr == null) return null;

        var monthlyRent = housingType switch
        {
            "1bed" => fmr.OneBedroom,
            "2bed" => fmr.TwoBedroom / 2,
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
