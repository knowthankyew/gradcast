using System.Text.Json;
using Dapper;
using GradCast.Api.Configuration;
using GradCast.Api.Models;
using GradCast.Data;
using Microsoft.Extensions.Options;

namespace GradCast.Api.Services;

/// <summary>
/// Job Pulse service that queries Adzuna API for live job market data.
/// Falls back to Scorecard-only data if Adzuna is unavailable or unconfigured.
/// </summary>
public class AdzunaJobPulseService : IJobPulseService
{
    private readonly HttpClient _httpClient;
    private readonly AdzunaOptions _options;
    private readonly ISqliteConnectionFactory _dbFactory;
    private readonly ILogger<AdzunaJobPulseService> _logger;

    public AdzunaJobPulseService(
        HttpClient httpClient,
        IOptions<AdzunaOptions> options,
        ISqliteConnectionFactory dbFactory,
        ILogger<AdzunaJobPulseService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<JobPulseResult?> GetPulseAsync(string cipCode, string cbsaCode, CancellationToken ct = default)
    {
        // Get location name for the search
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string locationSql = "SELECT name FROM cbsa_locations WHERE cbsa_code = @CbsaCode LIMIT 1;";
        var locationName = await conn.QueryFirstOrDefaultAsync<string>(
            new CommandDefinition(locationSql, new { CbsaCode = cbsaCode }, cancellationToken: ct));

        if (locationName == null) return null;

        // Get Scorecard median earnings for this CIP at any school (as baseline)
        var scorecardEarnings = await GetScorecardEarningsAsync(cipCode, ct);

        // Build search keywords from CIP code
        var displayKeywords = CipJobKeywordMap.GetSearchQuery(cipCode);
        var searchKeyword = CipJobKeywordMap.GetPrimaryKeyword(cipCode);

        // If Adzuna isn't configured, return Scorecard-only result
        if (string.IsNullOrEmpty(_options.AppId) || string.IsNullOrEmpty(_options.AppKey))
        {
            _logger.LogDebug("Adzuna not configured, returning Scorecard-only pulse");
            return new JobPulseResult(
                CipCode: cipCode,
                CbsaCode: cbsaCode,
                SearchKeywords: displayKeywords,
                ActiveOpenings: 0,
                LocalMedianSalary: null,
                ScorecardMedianEarnings: scorecardEarnings,
                DataSource: "scorecard_only",
                LocationName: locationName
            );
        }

        // Query Adzuna
        try
        {
            var locationQuery = ExtractCityForSearch(locationName);
            // Use + encoding for spaces (Adzuna requires this, not %20)
            var encodedKeyword = searchKeyword.Replace(" ", "+");
            var encodedLocation = locationQuery.Replace(" ", "+");
            var url = $"{_options.BaseUrl}/jobs/{_options.Country}/search/1" +
                      $"?app_id={_options.AppId}" +
                      $"&app_key={_options.AppKey}" +
                      $"&what={encodedKeyword}" +
                      $"&where={encodedLocation}" +
                      $"&results_per_page=0";

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Adzuna returned {Status} for CIP {Cip} in {Location}",
                    response.StatusCode, cipCode, locationQuery);

                return FallbackResult(cipCode, cbsaCode, displayKeywords, scorecardEarnings, locationName);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
            var json = await reader.ReadToEndAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var count = root.TryGetProperty("count", out var countEl) ? countEl.GetInt32() : 0;

            decimal? meanSalary = null;
            if (root.TryGetProperty("mean", out var meanEl) && meanEl.ValueKind == JsonValueKind.Number)
            {
                meanSalary = meanEl.GetDecimal();
            }

            _logger.LogInformation(
                "Fetched Adzuna pulse for CIP {CipCode} in {LocationName}: Openings={ActiveOpenings}, Salary={LocalMedianSalary}",
                cipCode, locationName, count, meanSalary);

            return new JobPulseResult(
                CipCode: cipCode,
                CbsaCode: cbsaCode,
                SearchKeywords: displayKeywords,
                ActiveOpenings: count,
                LocalMedianSalary: meanSalary,
                ScorecardMedianEarnings: scorecardEarnings,
                DataSource: "adzuna",
                LocationName: locationName
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Adzuna API call failed for CIP {Cip} in {Cbsa}. Message: {Message}", cipCode, cbsaCode, ex.Message);
            return FallbackResult(cipCode, cbsaCode, displayKeywords, scorecardEarnings, locationName);
        }
    }

    private JobPulseResult FallbackResult(string cipCode, string cbsaCode, string displayKeywords, decimal? scorecardEarnings, string locationName)
    {
        return new JobPulseResult(
            CipCode: cipCode,
            CbsaCode: cbsaCode,
            SearchKeywords: displayKeywords,
            ActiveOpenings: 0,
            LocalMedianSalary: null,
            ScorecardMedianEarnings: scorecardEarnings,
            DataSource: "scorecard_only",
            LocationName: locationName
        );
    }

    private async Task<decimal?> GetScorecardEarningsAsync(string cipCode, CancellationToken ct)
    {
        var prefix = cipCode.Length >= 2 ? cipCode[..2] : cipCode;

        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT CAST(median_earnings AS REAL)
            FROM programs
            WHERE cip_code LIKE @PrefixPattern AND median_earnings IS NOT NULL
            ORDER BY CAST(median_earnings AS REAL) ASC;
        """;

        var earnings = (await conn.QueryAsync<decimal>(
            new CommandDefinition(sql, new { PrefixPattern = prefix + "%" }, cancellationToken: ct))).ToList();

        if (earnings.Count == 0) return null;

        // Return the median (middle value when sorted)
        return earnings[earnings.Count / 2];
    }

    /// <summary>
    /// Extracts the primary city name from a CBSA name for Adzuna location search.
    /// "Atlanta-Sandy Springs-Alpharetta, GA" → "Atlanta"
    /// </summary>
    private static string ExtractCityForSearch(string cbsaName)
    {
        var firstDash = cbsaName.IndexOf('-');
        var firstComma = cbsaName.IndexOf(',');

        if (firstDash > 0 && firstDash < firstComma)
            return cbsaName[..firstDash].Trim();
        if (firstComma > 0)
            return cbsaName[..firstComma].Trim();

        return cbsaName;
    }
}
