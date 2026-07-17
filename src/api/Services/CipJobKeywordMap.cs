namespace GradCast.Api.Services;

/// <summary>
/// Maps CIP 2-digit category codes to job search keywords.
/// Used to translate academic program classifications into real job board queries.
/// </summary>
public static class CipJobKeywordMap
{
    private static readonly Dictionary<string, string[]> Map = new()
    {
        ["01"] = ["agriculture", "farm management", "horticulture", "agronomy"],
        ["03"] = ["environmental scientist", "conservation", "wildlife biologist", "forestry"],
        ["04"] = ["architect", "urban planner", "landscape architect"],
        ["05"] = ["policy analyst", "research associate", "cultural affairs"],
        ["09"] = ["journalist", "public relations", "communications specialist", "media"],
        ["10"] = ["broadcast technician", "audio engineer", "video production"],
        ["11"] = ["software engineer", "developer", "data analyst", "IT", "cybersecurity"],
        ["12"] = ["chef", "culinary", "hospitality manager", "cosmetologist"],
        ["13"] = ["teacher", "education administrator", "curriculum developer", "instructional designer"],
        ["14"] = ["engineer", "mechanical engineer", "civil engineer", "electrical engineer"],
        ["15"] = ["engineering technician", "CAD", "quality assurance", "industrial technology"],
        ["16"] = ["translator", "interpreter", "linguist", "foreign language"],
        ["19"] = ["nutritionist", "family counselor", "consumer science"],
        ["22"] = ["lawyer", "paralegal", "legal assistant", "attorney", "compliance"],
        ["23"] = ["writer", "editor", "technical writer", "content creator"],
        ["24"] = ["administrative assistant", "office manager", "general management"],
        ["25"] = ["librarian", "information specialist", "archivist"],
        ["26"] = ["biologist", "research scientist", "lab technician", "biotech"],
        ["27"] = ["mathematician", "statistician", "actuary", "data scientist"],
        ["28"] = ["military officer", "defense analyst"],
        ["30"] = ["research analyst", "interdisciplinary researcher"],
        ["31"] = ["recreation director", "fitness trainer", "sports management"],
        ["38"] = ["philosophy professor", "ethics consultant"],
        ["39"] = ["clergy", "chaplain", "religious director"],
        ["40"] = ["physicist", "chemist", "geologist", "materials scientist"],
        ["41"] = ["lab technician", "quality control", "science technician"],
        ["42"] = ["psychologist", "counselor", "therapist", "behavioral analyst"],
        ["43"] = ["police officer", "criminal justice", "homeland security", "forensic"],
        ["44"] = ["social worker", "public administrator", "nonprofit manager"],
        ["45"] = ["economist", "sociologist", "political scientist", "geographer"],
        ["46"] = ["electrician", "plumber", "carpenter", "construction manager"],
        ["47"] = ["mechanic", "automotive technician", "HVAC", "diesel technician"],
        ["48"] = ["machinist", "CNC operator", "welder", "manufacturing"],
        ["49"] = ["pilot", "logistics", "truck driver", "transportation manager"],
        ["50"] = ["graphic designer", "musician", "actor", "fine artist", "animator"],
        ["51"] = ["nurse", "physician assistant", "pharmacist", "medical", "healthcare"],
        ["52"] = ["business analyst", "accountant", "marketing", "finance", "management"],
        ["54"] = ["historian", "museum curator", "archivist", "research"],
    };

    /// <summary>
    /// Returns job search keywords for a given CIP code (uses 2-digit prefix).
    /// </summary>
    public static string[] GetKeywords(string cipCode)
    {
        var prefix = cipCode.Length >= 2 ? cipCode[..2] : cipCode;
        return Map.GetValueOrDefault(prefix, ["entry level", cipCode]);
    }

    /// <summary>
    /// Returns a search query string joining the top keywords for the CIP code.
    /// </summary>
    public static string GetSearchQuery(string cipCode, int maxTerms = 3)
    {
        var keywords = GetKeywords(cipCode);
        return string.Join(" OR ", keywords.Take(maxTerms));
    }
}
