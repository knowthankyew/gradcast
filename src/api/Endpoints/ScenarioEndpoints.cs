using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GradCast.Api.Data;
using GradCast.Api.Models;
using GradCast.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GradCast.Api.Endpoints;

public static partial class ScenarioEndpoints
{
    private const int MaxDailySharesPerClient = 50;
    private const string DefaultClientSalt = "gradcast-lakehouse-client-salt-v1";

    private static readonly HashSet<string> AllowedHousingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "alone", "roommate"
    };

    private static readonly HashSet<string> AllowedSentimentTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "comfortable", "surplus_plus", "tight", "hidden_gem", "stretch"
    };

    private static readonly HashSet<string> AllowedSalaryModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "scorecard_median", "user_override"
    };

    [GeneratedRegex("<.*?>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"^\d{2}(\.\d{4})?$", RegexOptions.Compiled)]
    private static partial Regex CipCodeRegex();

    [GeneratedRegex(@"^\d{5}$", RegexOptions.Compiled)]
    private static partial Regex CbsaCodeRegex();

    public static void MapScenarioEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scenarios");

        group.MapPost("/share", ShareScenario)
             .WithName("ShareScenario")
             .WithDescription("Share an anonymous positive financial scenario to edge storage");

        group.MapGet("/positive-finds", GetPositiveFinds)
             .WithName("GetPositiveFinds")
             .WithDescription("Retrieve top shared positive financial scenarios filtered by metro or CIP family");

        group.MapGet("/{id}", GetScenarioById)
             .WithName("GetScenarioById")
             .WithDescription("Retrieve a specific shared scenario by ID");
    }

    public static IResult? ValidateShareRequest(ShareScenarioRequest request)
    {
        if (request.SchoolId <= 0)
        {
            return ValidationFailure("Invalid school ID", "A valid school ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CipCode) || !CipCodeRegex().IsMatch(request.CipCode.Trim()))
        {
            return ValidationFailure("Invalid CIP code", "CIP code must be in 2-digit (XX) or 6-digit (XX.XXXX) format.");
        }

        if (string.IsNullOrWhiteSpace(request.CbsaCode) || !CbsaCodeRegex().IsMatch(request.CbsaCode.Trim()))
        {
            return ValidationFailure("Invalid CBSA code", "CBSA code must be a 5-digit numeric identifier.");
        }

        if (request.CredentialLevel < 1 || request.CredentialLevel > 6)
        {
            return ValidationFailure("Invalid credential level", "Credential level must be between 1 and 6.");
        }

        if (request.ReportedAnnualSalary < 0 || request.ReportedAnnualSalary > 2_000_000)
        {
            return ValidationFailure("Invalid salary", "Reported annual salary must be between $0 and $2,000,000.");
        }

        if (request.MonthlyDisposable < -20_000)
        {
            return ValidationFailure("Invalid disposable income", "Monthly disposable income cannot be below -$20,000.");
        }

        if (string.IsNullOrWhiteSpace(request.HousingType) || !AllowedHousingTypes.Contains(request.HousingType.Trim()))
        {
            return ValidationFailure("Invalid housing type", "Housing type must be either 'alone' or 'roommate'.");
        }

        if (string.IsNullOrWhiteSpace(request.SentimentTag) || !AllowedSentimentTags.Contains(request.SentimentTag.Trim()))
        {
            return ValidationFailure("Invalid sentiment tag", "Sentiment tag must be one of: comfortable, surplus_plus, tight, hidden_gem, stretch.");
        }

        if (!string.IsNullOrWhiteSpace(request.SalaryMode) && !AllowedSalaryModes.Contains(request.SalaryMode.Trim()))
        {
            return ValidationFailure("Invalid salary mode", "Salary mode must be either 'scorecard_median' or 'user_override'.");
        }

        if (request.ScenarioTitle != null && request.ScenarioTitle.Length > 60)
        {
            return ValidationFailure("Invalid scenario title", "Scenario title cannot exceed 60 characters.");
        }

        return null;
    }

    public static string ComputeClientHash(string clientIp, string? customSalt = null)
    {
        var salt = !string.IsNullOrWhiteSpace(customSalt) ? customSalt : DefaultClientSalt;
        var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var payload = $"{dateStr}:{clientIp}:{salt}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string ExtractClientIp(HttpContext httpContext)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;

        // Only trust forwarded edge headers when direct TCP connection is loopback (trusted reverse proxy hop)
        if (remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp))
        {
            // Fly.io trusted edge proxy header
            if (httpContext.Request.Headers.TryGetValue("Fly-Client-IP", out var flyIp))
            {
                var ipStr = flyIp.ToString().Trim();
                if (System.Net.IPAddress.TryParse(ipStr, out _))
                {
                    return ipStr;
                }
            }

            if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var firstIp = forwardedFor.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstIp) && System.Net.IPAddress.TryParse(firstIp, out _))
                {
                    return firstIp;
                }
            }
        }

        return remoteIp?.ToString() ?? "127.0.0.1";
    }

    private static async Task<IResult> ShareScenario(
        [FromBody] ShareScenarioRequest request,
        HttpContext httpContext,
        IScenarioRepository repository,
        CancellationToken ct)
    {
        var validationError = ValidateShareRequest(request);
        if (validationError != null)
        {
            return validationError;
        }

        var clientIp = ExtractClientIp(httpContext);
        var configuredSalt = httpContext.RequestServices.GetService<IConfiguration>()?["Security:ClientHashSalt"];
        var clientHash = ComputeClientHash(clientIp, configuredSalt);
        var todayPrefix = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var dailySubmissions = await repository.GetDailySubmissionCountByClientHashAsync(clientHash, todayPrefix, ct);
        if (dailySubmissions >= MaxDailySharesPerClient)
        {
            return Results.Problem(
                title: "Rate limit exceeded",
                detail: "Daily scenario share limit reached for this client. Please try again tomorrow.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var scenarioId = Guid.NewGuid().ToString("N");
        var sanitizedTitle = request.ScenarioTitle is not null
            ? HtmlTagRegex().Replace(request.ScenarioTitle, string.Empty).Trim()
            : null;

        var entity = new SharedScenario
        {
            Id = scenarioId,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            SchoolId = request.SchoolId,
            CipCode = request.CipCode.Trim(),
            CredentialLevel = request.CredentialLevel,
            CbsaCode = request.CbsaCode.Trim(),
            HousingType = request.HousingType.Trim().ToLowerInvariant(),
            SalaryMode = request.SalaryMode?.Trim().ToLowerInvariant() ?? "scorecard_median",
            ReportedAnnualSalary = request.ReportedAnnualSalary,
            MonthlyNetTakeHome = request.MonthlyNetTakeHome,
            MonthlyRent = request.MonthlyRent,
            MonthlyLoanPayment = request.MonthlyLoanPayment,
            MonthlyDisposable = request.MonthlyDisposable,
            DisposableRatio = request.DisposableRatio,
            SentimentTag = request.SentimentTag.Trim().ToLowerInvariant(),
            ScenarioTitle = string.IsNullOrWhiteSpace(sanitizedTitle) ? null : sanitizedTitle,
            ClientHash = clientHash
        };

        await repository.InsertScenarioAsync(entity, ct);

        return Results.Created($"/api/scenarios/{scenarioId}", new ShareScenarioResponse(
            Id: scenarioId,
            Message: "Scenario shared successfully."
        ));
    }

    private static async Task<IResult> GetPositiveFinds(
        [FromQuery] string? cbsaCode,
        [FromQuery] string? cipFamily,
        [FromQuery] int limit = 20,
        IScenarioRepository repository = default!,
        CancellationToken ct = default)
    {
        var results = await repository.GetPositiveFindsAsync(cbsaCode, cipFamily, limit, ct);
        return Results.Ok(results);
    }

    private static async Task<IResult> GetScenarioById(
        string id,
        IScenarioRepository repository,
        CancellationToken ct)
    {
        var scenario = await repository.GetByIdAsync(id, ct);
        return scenario is not null ? Results.Ok(scenario) : Results.NotFound();
    }

    private static IResult ValidationFailure(string title, string detail, int statusCode = StatusCodes.Status400BadRequest)
        => Results.Problem(title: title, detail: detail, statusCode: statusCode);
}
