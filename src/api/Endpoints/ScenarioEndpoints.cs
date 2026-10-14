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
    // Development fallback salt strictly for offline unit tests; production requires Security:ClientHashSalt
    private const string DevelopmentFallbackSalt = "gradcast-lakehouse-dev-test-salt";

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

    [GeneratedRegex(@"^\d{2}$", RegexOptions.Compiled)]
    private static partial Regex CipFamilyRegex();

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

        if (request.MonthlyDisposable is < -20_000 or > 200_000)
        {
            return ValidationFailure("Invalid disposable income", "Monthly disposable income must be between -$20,000 and $200,000.");
        }

        if (request.MonthlyNetTakeHome is < 0 or > 200_000)
        {
            return ValidationFailure("Invalid take-home pay", "Monthly net take-home pay must be between $0 and $200,000.");
        }

        if (request.MonthlyRent is < 0 or > 50_000)
        {
            return ValidationFailure("Invalid rent", "Monthly rent must be between $0 and $50,000.");
        }

        if (request.MonthlyLoanPayment is < 0 or > 50_000)
        {
            return ValidationFailure("Invalid loan payment", "Monthly loan payment must be between $0 and $50,000.");
        }

        if (double.IsNaN(request.DisposableRatio) || double.IsInfinity(request.DisposableRatio) || request.DisposableRatio is < -100.0 or > 100.0)
        {
            return ValidationFailure("Invalid disposable ratio", "Disposable ratio is out of range.");
        }

        if ((long)request.MonthlyDisposable != (long)request.MonthlyNetTakeHome - request.MonthlyRent - request.MonthlyLoanPayment)
        {
            return ValidationFailure("Inconsistent financials", "Monthly disposable must equal net take-home minus rent and loan payment.");
        }

        if (string.IsNullOrWhiteSpace(request.HousingType) || !AllowedHousingTypes.Contains(request.HousingType.Trim()))
        {
            return ValidationFailure("Invalid housing type", "Housing type must be either 'alone' or 'roommate'.");
        }

        if (string.IsNullOrWhiteSpace(request.SentimentTag) || !AllowedSentimentTags.Contains(request.SentimentTag.Trim()))
        {
            return ValidationFailure("Invalid sentiment tag", "Sentiment tag must be one of: comfortable, surplus_plus, tight, hidden_gem, stretch.");
        }

        if (string.IsNullOrWhiteSpace(request.SalaryMode) || !AllowedSalaryModes.Contains(request.SalaryMode.Trim()))
        {
            return ValidationFailure("Invalid salary mode", "Salary mode must be either 'scorecard_median' or 'user_override'.");
        }

        if (request.ScenarioTitle != null && request.ScenarioTitle.Length > 60)
        {
            return ValidationFailure("Invalid scenario title", "Scenario title cannot exceed 60 characters.");
        }

        return null;
    }

    public static string ComputeClientHash(string clientIp, string? customSalt = null, string? datePrefixUtc = null)
    {
        var salt = !string.IsNullOrWhiteSpace(customSalt) ? customSalt : DevelopmentFallbackSalt;
        var dateStr = !string.IsNullOrWhiteSpace(datePrefixUtc) ? datePrefixUtc : DateTime.UtcNow.ToString("yyyy-MM-dd");
        var payload = $"{dateStr}:{clientIp}:{salt}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool IsTrustedProxy(System.Net.IPAddress? ip)
    {
        if (ip is null) return false;
        if (System.Net.IPAddress.IsLoopback(ip)) return true;
        // Fly.io internal mesh / private wireguard proxy network (fdaa::/16)
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var bytes = ip.GetAddressBytes();
            return bytes.Length >= 2 && bytes[0] == 0xfd && bytes[1] == 0xaa;
        }
        return false;
    }

    public static string ExtractClientIp(HttpContext httpContext)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;

        // Only trust forwarded edge headers when direct TCP connection is a trusted reverse proxy hop (loopback or Fly mesh)
        if (IsTrustedProxy(remoteIp))
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
        var config = httpContext.RequestServices.GetService<IConfiguration>();
        var env = httpContext.RequestServices.GetService<IWebHostEnvironment>();
        var configuredSalt = config?["Security:ClientHashSalt"];

        if (string.IsNullOrWhiteSpace(configuredSalt))
        {
            if (env != null && env.IsProduction())
            {
                throw new InvalidOperationException("Security:ClientHashSalt must be configured in production to protect client privacy.");
            }
            configuredSalt = DevelopmentFallbackSalt;
        }

        var nowUtc = DateTime.UtcNow;
        var todayPrefix = nowUtc.ToString("yyyy-MM-dd");
        var clientHash = ComputeClientHash(clientIp, configuredSalt, todayPrefix);

        var scenarioId = Guid.NewGuid().ToString("N");
        var sanitizedTitle = request.ScenarioTitle is not null
            ? HtmlTagRegex().Replace(request.ScenarioTitle, string.Empty).Trim()
            : null;

        var entity = new SharedScenario
        {
            Id = scenarioId,
            CreatedAt = nowUtc.ToString("o"),
            SchoolId = request.SchoolId,
            CipCode = request.CipCode.Trim(),
            CredentialLevel = request.CredentialLevel,
            CbsaCode = request.CbsaCode.Trim(),
            HousingType = request.HousingType.Trim().ToLowerInvariant(),
            SalaryMode = string.IsNullOrWhiteSpace(request.SalaryMode) ? "scorecard_median" : request.SalaryMode.Trim().ToLowerInvariant(),
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

        var inserted = await repository.TryInsertScenarioWithDailyLimitAsync(entity, MaxDailySharesPerClient, todayPrefix, ct);
        if (!inserted)
        {
            return Results.Problem(
                title: "Rate limit exceeded",
                detail: "Daily scenario share limit reached for this client. Please try again tomorrow.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

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
        if (!string.IsNullOrWhiteSpace(cbsaCode) && !CbsaCodeRegex().IsMatch(cbsaCode.Trim()))
        {
            return ValidationFailure("Invalid CBSA code", "CBSA code must be a 5-digit FIPS metro identifier.");
        }

        if (!string.IsNullOrWhiteSpace(cipFamily) && !CipFamilyRegex().IsMatch(cipFamily.Trim()))
        {
            return ValidationFailure("Invalid CIP family", "CIP family must be a 2-digit code (e.g., '11').");
        }

        var clampedLimit = Math.Clamp(limit, 1, 100);
        var results = await repository.GetPositiveFindsAsync(cbsaCode?.Trim(), cipFamily?.Trim(), clampedLimit, ct);
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
