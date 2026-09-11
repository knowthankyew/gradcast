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

        group.MapGet("/{cbsaCode}/housing", GetHousingCost)
             .WithName("GetHousingCost")
             .WithDescription("Get Fair Market Rent for a metro area");
    }

    private static async Task<IResult> SearchLocations(
        [FromQuery(Name = "q")] string? query,
        [FromQuery(Name = "requireHousing")] bool? requireHousing,
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

        var results = await service.SearchLocationsAsync(query, requireHousing ?? false, ct);
        return Results.Ok(results);
    }

    private static async Task<IResult> GetHousingCost(
        string cbsaCode,
        [FromQuery(Name = "type")] string? housingType,
        IHousingCostService service,
        CancellationToken ct)
    {
        var type = housingType ?? "1bed";
        if (type != "1bed" && type != "2bed" && type != "studio")
        {
            return Results.Problem(
                title: "Invalid housing type",
                detail: "Housing type must be 'studio', '1bed', or '2bed'.",
                statusCode: 400);
        }

        var result = await service.GetHousingCostAsync(cbsaCode, type, ct);
        if (result == null)
        {
            return Results.Problem(
                title: "No rent data",
                detail: $"No Fair Market Rent data found for CBSA code '{cbsaCode}'.",
                statusCode: 404);
        }

        return Results.Ok(result);
    }
}
