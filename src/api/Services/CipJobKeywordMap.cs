namespace GradCast.Api.Services;

/// <summary>
/// Maps CIP codes to job search keywords at two granularity levels:
/// - 4-digit (e.g., "1107" → "software engineer") for precision
/// - 2-digit fallback (e.g., "11" → "developer, IT") for coverage
/// </summary>
public static class CipJobKeywordMap
{
    /// <summary>
    /// Returns job search keywords for a given CIP code.
    /// Tries 4-digit first for precision, falls back to 2-digit category.
    /// </summary>
    public static string[] GetKeywords(string cipCode)
    {
        var normalized = cipCode.Replace(".", "");

        if (normalized.Length >= 4)
        {
            var fourDigit = normalized[..4];
            if (FourDigitMap.TryGetValue(fourDigit, out var specific))
                return specific;
        }

        var prefix = normalized.Length >= 2 ? normalized[..2] : normalized;
        return TwoDigitMap.GetValueOrDefault(prefix, ["entry level", cipCode]);
    }

    /// <summary>
    /// Returns a search query string joining the top keywords.
    /// </summary>
    public static string GetSearchQuery(string cipCode, int maxTerms = 3)
    {
        var keywords = GetKeywords(cipCode);
        return string.Join(" OR ", keywords.Take(maxTerms));
    }

    // ─── 4-digit CIP → specific job keywords ──────────────────────────────────

    private static readonly Dictionary<string, string[]> FourDigitMap = new()
    {
        // Computer & Information Sciences (11xx)
        ["1101"] = ["information systems analyst", "IT analyst", "systems administrator"],
        ["1102"] = ["software developer", "programmer", "application developer"],
        ["1104"] = ["information scientist", "data architect", "knowledge manager"],
        ["1107"] = ["software engineer", "computer scientist", "machine learning engineer"],
        ["1108"] = ["cybersecurity analyst", "information security", "penetration tester"],
        ["1109"] = ["network engineer", "systems engineer", "cloud architect"],
        ["1110"] = ["IT manager", "technology director", "CTO"],

        // Engineering (14xx)
        ["1405"] = ["biomedical engineer", "medical device engineer"],
        ["1407"] = ["chemical engineer", "process engineer"],
        ["1408"] = ["civil engineer", "structural engineer", "transportation engineer"],
        ["1409"] = ["computer engineer", "hardware engineer", "embedded systems"],
        ["1410"] = ["electrical engineer", "electronics engineer", "power systems"],
        ["1414"] = ["environmental engineer", "sustainability engineer"],
        ["1419"] = ["mechanical engineer", "design engineer", "thermal engineer"],
        ["1435"] = ["industrial engineer", "manufacturing engineer"],
        ["1437"] = ["operations research analyst", "optimization engineer"],

        // Business (52xx)
        ["5202"] = ["business administrator", "operations manager", "general manager"],
        ["5203"] = ["accountant", "CPA", "auditor", "tax accountant"],
        ["5206"] = ["economist", "business economist", "financial analyst"],
        ["5207"] = ["entrepreneur", "startup founder", "business development"],
        ["5208"] = ["financial analyst", "investment banker", "portfolio manager"],
        ["5210"] = ["HR manager", "recruiter", "talent acquisition"],
        ["5212"] = ["MIS analyst", "IT business analyst", "systems analyst"],
        ["5213"] = ["data scientist", "quantitative analyst", "business intelligence"],
        ["5214"] = ["marketing manager", "digital marketing", "brand manager"],

        // Health Professions (51xx)
        ["5107"] = ["health administrator", "hospital administrator"],
        ["5114"] = ["physician", "medical doctor", "resident"],
        ["5116"] = ["nurse", "registered nurse", "nurse practitioner"],
        ["5120"] = ["pharmacist", "pharmacy technician"],
        ["5122"] = ["public health analyst", "epidemiologist", "health educator"],

        // Education (13xx)
        ["1301"] = ["teacher", "educator", "instructional coordinator"],
        ["1303"] = ["curriculum developer", "instructional designer"],
        ["1304"] = ["school principal", "education administrator"],
        ["1305"] = ["educational technology specialist", "e-learning developer"],
        ["1311"] = ["school counselor", "guidance counselor", "career advisor"],

        // Biological Sciences (26xx)
        ["2601"] = ["biologist", "research scientist", "lab researcher"],
        ["2602"] = ["biochemist", "molecular biologist"],
        ["2611"] = ["bioinformatics analyst", "computational biologist"],
        ["2615"] = ["neuroscientist", "brain researcher"],

        // Psychology (42xx)
        ["4201"] = ["psychologist", "behavioral scientist", "research psychologist"],
        ["4228"] = ["industrial-organizational psychologist", "HR consultant"],

        // Social Sciences (45xx)
        ["4506"] = ["economist", "economic analyst", "policy analyst"],
        ["4510"] = ["political scientist", "policy advisor", "government analyst"],

        // Arts (50xx)
        ["5004"] = ["graphic designer", "UX designer", "visual designer"],
        ["5006"] = ["cinematographer", "film editor", "video producer"],
        ["5009"] = ["musician", "music producer", "audio engineer"],

        // Legal (22xx)
        ["2201"] = ["attorney", "lawyer", "associate attorney"],
        ["2202"] = ["legal researcher", "law clerk", "compliance officer"],

        // Mathematics (27xx)
        ["2701"] = ["mathematician", "quantitative analyst"],
        ["2703"] = ["applied mathematician", "data scientist", "operations researcher"],
        ["2705"] = ["statistician", "biostatistician", "data analyst"],
    };

    // ─── 2-digit CIP → broader category keywords (fallback) ───────────────────

    private static readonly Dictionary<string, string[]> TwoDigitMap = new()
    {
        ["01"] = ["agriculture", "farm management", "horticulture"],
        ["03"] = ["environmental scientist", "conservation", "wildlife biologist"],
        ["04"] = ["architect", "urban planner", "landscape architect"],
        ["05"] = ["policy analyst", "research associate", "cultural affairs"],
        ["09"] = ["journalist", "public relations", "communications specialist"],
        ["10"] = ["broadcast technician", "audio engineer", "video production"],
        ["11"] = ["software engineer", "developer", "data analyst", "IT"],
        ["12"] = ["chef", "culinary", "hospitality manager"],
        ["13"] = ["teacher", "education administrator", "curriculum developer"],
        ["14"] = ["engineer", "mechanical engineer", "civil engineer"],
        ["15"] = ["engineering technician", "CAD", "quality assurance"],
        ["16"] = ["translator", "interpreter", "linguist"],
        ["19"] = ["nutritionist", "family counselor", "consumer science"],
        ["22"] = ["lawyer", "paralegal", "attorney", "compliance"],
        ["23"] = ["writer", "editor", "technical writer", "content creator"],
        ["24"] = ["administrative assistant", "office manager"],
        ["25"] = ["librarian", "information specialist", "archivist"],
        ["26"] = ["biologist", "research scientist", "lab technician"],
        ["27"] = ["mathematician", "statistician", "actuary", "data scientist"],
        ["28"] = ["military officer", "defense analyst"],
        ["30"] = ["research analyst", "interdisciplinary researcher"],
        ["31"] = ["recreation director", "fitness trainer", "sports management"],
        ["38"] = ["philosophy professor", "ethics consultant"],
        ["39"] = ["clergy", "chaplain", "religious director"],
        ["40"] = ["physicist", "chemist", "geologist", "materials scientist"],
        ["41"] = ["lab technician", "quality control", "science technician"],
        ["42"] = ["psychologist", "counselor", "therapist"],
        ["43"] = ["police officer", "criminal justice", "homeland security"],
        ["44"] = ["social worker", "public administrator", "nonprofit manager"],
        ["45"] = ["economist", "sociologist", "political scientist"],
        ["46"] = ["electrician", "plumber", "carpenter", "construction manager"],
        ["47"] = ["mechanic", "automotive technician", "HVAC"],
        ["48"] = ["machinist", "CNC operator", "welder", "manufacturing"],
        ["49"] = ["pilot", "logistics", "transportation manager"],
        ["50"] = ["graphic designer", "musician", "actor", "animator"],
        ["51"] = ["nurse", "physician assistant", "pharmacist", "healthcare"],
        ["52"] = ["business analyst", "accountant", "marketing", "finance"],
        ["54"] = ["historian", "museum curator", "archivist"],
    };
}
