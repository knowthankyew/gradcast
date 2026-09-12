namespace GradCast.Api.Services;

/// <summary>
/// Canonical lookup tables for College Scorecard categorical codes.
/// Shared by both <see cref="CollegeScorecardService"/> and
/// <see cref="LocalCollegeScorecardService"/> so the mappings stay in sync.
/// </summary>
public static class ScorecardLookups
{
    public static readonly IReadOnlyDictionary<int, string> CredentialLevels = new Dictionary<int, string>
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

    public static readonly IReadOnlyDictionary<int, string> OwnershipTypes = new Dictionary<int, string>
    {
        [1] = "Public",
        [2] = "Private Nonprofit",
        [3] = "Private For-Profit"
    };
}
