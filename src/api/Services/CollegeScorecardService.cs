using System.Net;
using System.Text.Json;
using GradCast.Api.Configuration;
using GradCast.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GradCast.Api.Services;

public class CollegeScorecardService : ICollegeScorecardService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly CollegeScorecardOptions _options;
    private readonly ILogger<CollegeScorecardService> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<int, string> CredentialLevels = new()
    {
        [1] = "Undergraduate Certificate",
        [2] = "Associate's Degree",
        [3] = "Bachelor's Degree",
        [4] = "Post-baccalaureate Certificate",
        [5] = "Master's Degree",
        [6] = "Doctoral Degree",
        [7] = "First Professional Degree",
        [8] = "Graduate Certificate"
    };

    private static readonly Dictionary<int, string> OwnershipTypes = new()
    {
        [1] = "Public",
        [2] = "Private Nonprofit",
        [3] = "Private For-Profit"
    };

    public CollegeScorecardService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<CollegeScorecardOptions> options,
        ILogger<CollegeScorecardService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SchoolSearchResult>> SearchSchoolsAsync(
        string query, string? state, CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl}/schools?api_key={_options.ApiKey}" +
                  $"&school.name={Uri.EscapeDataString(query)}" +
                  $"&fields=id,school.name,school.city,school.state" +
                  $"&per_page=10";

        if (!string.IsNullOrWhiteSpace(state))
        {
            url += $"&school.state={Uri.EscapeDataString(state)}";
        }

        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessOrThrow(response);

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");

        var schools = new List<SchoolSearchResult>();
        foreach (var item in results.EnumerateArray())
        {
            schools.Add(new SchoolSearchResult(
                Id: item.GetProperty("id").GetInt32(),
                Name: item.GetProperty("school.name").GetString() ?? "",
                City: item.GetProperty("school.city").GetString() ?? "",
                State: item.GetProperty("school.state").GetString() ?? ""
            ));
        }

        return schools;
    }

    public async Task<SchoolDetail?> GetSchoolDetailAsync(int schoolId, int? year = null, CancellationToken ct = default)
    {
        var dataPrefix = year.HasValue ? year.Value.ToString() : "latest";
        var cacheKey = $"school_detail_{schoolId}_{dataPrefix}";
        if (_cache.TryGetValue(cacheKey, out SchoolDetail? cached))
        {
            return cached;
        }

        var fields = string.Join(",",
            "id",
            "school.name",
            "school.city",
            "school.state",
            "school.school_url",
            "school.ownership",
            $"{dataPrefix}.admissions.admission_rate.overall",
            $"{dataPrefix}.student.size",
            $"{dataPrefix}.cost.tuition.in_state",
            $"{dataPrefix}.cost.tuition.out_of_state",
            $"{dataPrefix}.completion.rate_suppressed.overall",
            $"{dataPrefix}.programs.cip_4_digit"
        );

        var url = $"{_options.BaseUrl}/schools?api_key={_options.ApiKey}" +
                  $"&id={schoolId}" +
                  $"&fields={fields}";

        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessOrThrow(response);

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");

        if (results.GetArrayLength() == 0)
        {
            return null;
        }

        var item = results[0];
        var detail = MapSchoolDetail(item, dataPrefix);

        _cache.Set(cacheKey, detail, CacheDuration);
        return detail;
    }

    private SchoolDetail MapSchoolDetail(JsonElement item, string dataPrefix)
    {
        var ownership = GetIntOrDefault(item, "school.ownership");
        var programs = MapPrograms(item, dataPrefix);

        return new SchoolDetail(
            Id: item.GetProperty("id").GetInt32(),
            Name: GetStringOrDefault(item, "school.name"),
            City: GetStringOrDefault(item, "school.city"),
            State: GetStringOrDefault(item, "school.state"),
            SchoolUrl: GetStringOrNull(item, "school.school_url"),
            Ownership: ownership,
            OwnershipName: OwnershipTypes.GetValueOrDefault(ownership, "Unknown"),
            AdmissionRate: GetDecimalOrNull(item, $"{dataPrefix}.admissions.admission_rate.overall"),
            StudentSize: GetNullableInt(item, $"{dataPrefix}.student.size"),
            TuitionInState: GetNullableInt(item, $"{dataPrefix}.cost.tuition.in_state"),
            TuitionOutOfState: GetNullableInt(item, $"{dataPrefix}.cost.tuition.out_of_state"),
            CompletionRate: GetDecimalOrNull(item, $"{dataPrefix}.completion.rate_suppressed.overall"),
            Programs: programs
        );
    }

    private List<ProgramData> MapPrograms(JsonElement item, string dataPrefix)
    {
        var programs = new List<ProgramData>();

        if (!item.TryGetProperty($"{dataPrefix}.programs.cip_4_digit", out var programsArray) ||
            programsArray.ValueKind != JsonValueKind.Array)
        {
            return programs;
        }

        foreach (var prog in programsArray.EnumerateArray())
        {
            var code = GetStringOrDefault(prog, "code");
            var title = GetStringOrDefault(prog, "title");
            var credLevel = GetIntOrDefault(prog, "credential.level");
            var completions = GetNullableInt(prog, "counts.ipeds_awards1");

            decimal? earnings = null;
            if (prog.TryGetProperty("earnings", out var earningsObj) &&
                earningsObj.ValueKind == JsonValueKind.Object &&
                earningsObj.TryGetProperty("1_yr", out var oneYr) &&
                oneYr.ValueKind == JsonValueKind.Object &&
                oneYr.TryGetProperty("overall_median_earnings", out var medianEarnings) &&
                medianEarnings.ValueKind == JsonValueKind.Number)
            {
                earnings = medianEarnings.GetDecimal();
            }

            programs.Add(new ProgramData(
                Code: code,
                Title: title,
                CredentialLevel: credLevel,
                CredentialName: CredentialLevels.GetValueOrDefault(credLevel, "Unknown"),
                Completions: completions,
                MedianEarnings: earnings
            ));
        }

        return programs;
    }

    private static async Task EnsureSuccessOrThrow(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new CollegeScorecardRateLimitException(
                response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
        }

        throw new CollegeScorecardApiException(
            $"Scorecard API returned {(int)response.StatusCode}: {body}",
            response.StatusCode);
    }

    private static string GetStringOrDefault(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";

    private static string? GetStringOrNull(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString()
            : null;

    private static int GetIntOrDefault(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number
            ? val.GetInt32()
            : 0;

    private static int? GetNullableInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number
            ? val.GetInt32()
            : null;

    private static decimal? GetDecimalOrNull(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number
            ? val.GetDecimal()
            : null;
}

public class CollegeScorecardApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public CollegeScorecardApiException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public class CollegeScorecardRateLimitException : Exception
{
    public TimeSpan RetryAfter { get; }

    public CollegeScorecardRateLimitException(TimeSpan retryAfter)
        : base("College Scorecard API rate limit exceeded.")
    {
        RetryAfter = retryAfter;
    }
}
