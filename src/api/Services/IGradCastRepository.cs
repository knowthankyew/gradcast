using GradCast.Data.Entities;

namespace GradCast.Api.Services;

public interface IGradCastRepository
{
    Task<CbsaLocation?> GetLocationByCbsaCodeAsync(string cbsaCode, CancellationToken ct = default);
    Task<FairMarketRent?> GetLatestFairMarketRentAsync(string cbsaCode, CancellationToken ct = default);
    Task<decimal?> GetProgramMedianEarningsAsync(int schoolId, string cipCode, CancellationToken ct = default);
    Task<List<decimal>> GetSchoolMedianEarningsAsync(int schoolId, CancellationToken ct = default);
    Task<decimal> GetMedianDebtAsync(int schoolId, CancellationToken ct = default);
}
