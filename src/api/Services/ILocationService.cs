using GradCast.Api.Models;

namespace GradCast.Api.Services;

public interface ILocationService
{
    Task<IReadOnlyList<LocationSearchResult>> SearchLocationsAsync(
        string query,
        bool requireHousing = false,
        CancellationToken ct = default);

    Task<LocationSearchResult?> GetLocationByCbsaAsync(
        string cbsaCode,
        CancellationToken ct = default);
}
