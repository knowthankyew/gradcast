using GradCast.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GradCast.Api.Endpoints;

public static class SchoolEndpoints
{
    public static void MapSchoolEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/schools");

        group.MapGet("/search", SearchSchools)
             .WithName("SearchSchools")
             .WithDescription("Search schools by name with optional state filter");

        group.MapGet("/{id:int}", GetSchoolDetail)
             .WithName("GetSchoolDetail")
             .WithDescription("Get detailed school information including programs");

        group.MapGet("/{id:int}/tuition-trend", GetTuitionTrend)
             .WithName("GetTuitionTrend")
             .WithDescription("Get 5-year tuition trend data for a school");
    }

    private static async Task<IResult> SearchSchools(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? state,
        ICollegeScorecardService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return Results.Problem(
                title: "Invalid query",
                detail: "Search query must be at least 2 characters.",
                statusCode: 400);
        }

        try
        {
            var results = await service.SearchSchoolsAsync(query, state, ct);
            return Results.Ok(results);
        }
        catch (CollegeScorecardRateLimitException ex)
        {
            return Results.Problem(
                title: "Rate limit exceeded",
                detail: "The upstream data provider is temporarily unavailable. Please try again later.",
                statusCode: 503,
                extensions: new Dictionary<string, object?>
                {
                    ["retryAfterSeconds"] = (int)ex.RetryAfter.TotalSeconds
                });
        }
        catch (CollegeScorecardApiException)
        {
            return Results.Problem(
                title: "Upstream API error",
                detail: "Unable to retrieve data from College Scorecard.",
                statusCode: 502);
        }
    }

    private static async Task<IResult> GetSchoolDetail(
        int id,
        [FromQuery] int? year,
        ICollegeScorecardService service,
        CancellationToken ct)
    {
        if (year.HasValue && (year.Value < 2000 || year.Value > DateTime.UtcNow.Year))
        {
            return Results.Problem(
                title: "Invalid year",
                detail: $"Year must be between 2000 and {DateTime.UtcNow.Year}.",
                statusCode: 400);
        }

        try
        {
            var detail = await service.GetSchoolDetailAsync(id, year, ct);
            if (detail is null)
            {
                return Results.Problem(
                    title: "School not found",
                    detail: $"No school found with ID {id}.",
                    statusCode: 404);
            }

            return Results.Ok(detail);
        }
        catch (CollegeScorecardRateLimitException ex)
        {
            return Results.Problem(
                title: "Rate limit exceeded",
                detail: "The upstream data provider is temporarily unavailable. Please try again later.",
                statusCode: 503,
                extensions: new Dictionary<string, object?>
                {
                    ["retryAfterSeconds"] = (int)ex.RetryAfter.TotalSeconds
                });
        }
        catch (CollegeScorecardApiException)
        {
            return Results.Problem(
                title: "Upstream API error",
                detail: "Unable to retrieve data from College Scorecard.",
                statusCode: 502);
        }
    }

    private static async Task<IResult> GetTuitionTrend(
        int id,
        ICollegeScorecardService service,
        CancellationToken ct)
    {
        try
        {
            var trend = await service.GetTuitionTrendAsync(id, ct);
            return Results.Ok(trend);
        }
        catch (CollegeScorecardRateLimitException ex)
        {
            return Results.Problem(
                title: "Rate limit exceeded",
                detail: "The upstream data provider is temporarily unavailable. Please try again later.",
                statusCode: 503,
                extensions: new Dictionary<string, object?>
                {
                    ["retryAfterSeconds"] = (int)ex.RetryAfter.TotalSeconds
                });
        }
        catch (CollegeScorecardApiException)
        {
            return Results.Problem(
                title: "Upstream API error",
                detail: "Unable to retrieve data from College Scorecard.",
                statusCode: 502);
        }
    }
}
