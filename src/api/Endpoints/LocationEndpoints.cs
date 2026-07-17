using GradCast.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GradCast.Api.Endpoints;

public static class LocationEndpoints
{
    public static void MapLocationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/locations");

        group.MapGet("/search", SearchLocations)
             .WithName("SearchLocations")
             .WithDescription("Search metro areas by name");
    }

    private static async Task<IResult> SearchLocations(
        [FromQuery(Name = "q")] string? query,
        LocationService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return Results.Problem(
                title: "Invalid query",
                detail: "Search query must be at least 2 characters.",
                statusCode: 400);
        }

        var results = await service.SearchLocationsAsync(query, ct);
        return Results.Ok(results);
    }
}
