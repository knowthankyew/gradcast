using GradCast.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GradCast.Api.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs");

        group.MapGet("/pulse", GetJobPulse)
             .WithName("GetJobPulse")
             .WithDescription("Get job market data for a program field in a metro area");
    }

    private static async Task<IResult> GetJobPulse(
        [FromQuery] string cipCode,
        [FromQuery] string cbsa,
        IJobPulseService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cipCode) || cipCode.Length < 2)
        {
            return Results.Problem(
                title: "Invalid CIP code",
                detail: "CIP code must be at least 2 characters.",
                statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(cbsa))
        {
            return Results.Problem(
                title: "Invalid CBSA code",
                detail: "A CBSA metro area code is required.",
                statusCode: 400);
        }

        var result = await service.GetPulseAsync(cipCode, cbsa, ct);
        if (result == null)
        {
            return Results.Problem(
                title: "Location not found",
                detail: $"No location data for CBSA code '{cbsa}'.",
                statusCode: 404);
        }

        return Results.Ok(result);
    }
}
