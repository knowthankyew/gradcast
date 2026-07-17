using GradCast.Api.Models;

namespace GradCast.Api.Services;

public interface IJobPulseService
{
    Task<JobPulseResult?> GetPulseAsync(string cipCode, string cbsaCode, CancellationToken ct = default);
}
