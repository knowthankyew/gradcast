using GradCast.Api.Models;

namespace GradCast.Api.Services;

public interface IHousingCostService
{
    Task<HousingCostResult?> GetHousingCostAsync(
        string cbsaCode,
        string housingType,
        CancellationToken ct = default);
}
