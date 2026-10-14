using System.ComponentModel.DataAnnotations;

namespace GradCast.Api.Models;

/// <summary>
/// Inbound payload for submitting a user-shared financial scenario.
/// </summary>
public record ShareScenarioRequest(
    [Range(1, int.MaxValue, ErrorMessage = "A valid school ID is required.")]
    int SchoolId,

    [Required(ErrorMessage = "CIP code is required.")]
    [StringLength(20, ErrorMessage = "CIP code cannot exceed 20 characters.")]
    string CipCode,

    [Range(1, 6, ErrorMessage = "Credential level must be between 1 and 6.")]
    int CredentialLevel,

    [Required(ErrorMessage = "CBSA code is required.")]
    [StringLength(10, ErrorMessage = "CBSA code cannot exceed 10 characters.")]
    string CbsaCode,

    [Required(ErrorMessage = "Housing type is required.")]
    [StringLength(50, ErrorMessage = "Housing type cannot exceed 50 characters.")]
    string HousingType,

    [Required(ErrorMessage = "Salary mode is required.")]
    [StringLength(50, ErrorMessage = "Salary mode cannot exceed 50 characters.")]
    string SalaryMode,

    [Range(0, 2_000_000, ErrorMessage = "Reported annual salary must be between $0 and $2,000,000.")]
    int ReportedAnnualSalary,

    int MonthlyNetTakeHome,

    [Range(0, 50_000, ErrorMessage = "Monthly rent must be non-negative.")]
    int MonthlyRent,

    [Range(0, 50_000, ErrorMessage = "Monthly loan payment must be non-negative.")]
    int MonthlyLoanPayment,

    [Range(-20_000, 1_000_000, ErrorMessage = "Monthly disposable cannot be below -$20,000.")]
    int MonthlyDisposable,

    double DisposableRatio,

    [Required(ErrorMessage = "Sentiment tag is required.")]
    [StringLength(50, ErrorMessage = "Sentiment tag cannot exceed 50 characters.")]
    string SentimentTag,

    [StringLength(60, ErrorMessage = "Scenario title cannot exceed 60 characters.")]
    string? ScenarioTitle
);

/// <summary>
/// Public DTO representing an anonymized shared positive-find scenario.
/// Excludes internal anti-abuse client hashes.
/// </summary>
public class SharedScenarioDto
{
    public string Id { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string CipCode { get; set; } = string.Empty;
    public int CredentialLevel { get; set; }
    public string CbsaCode { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string HousingType { get; set; } = string.Empty;
    public string SalaryMode { get; set; } = string.Empty;
    public int ReportedAnnualSalary { get; set; }
    public int MonthlyNetTakeHome { get; set; }
    public int MonthlyRent { get; set; }
    public int MonthlyLoanPayment { get; set; }
    public int MonthlyDisposable { get; set; }
    public double DisposableRatio { get; set; }
    public string SentimentTag { get; set; } = string.Empty;
    public string? ScenarioTitle { get; set; }

    public SharedScenarioDto() { }

    public SharedScenarioDto(
        string id,
        string createdAt,
        int schoolId,
        string? schoolName,
        string cipCode,
        int credentialLevel,
        string cbsaCode,
        string? locationName,
        string housingType,
        string salaryMode,
        int reportedAnnualSalary,
        int monthlyNetTakeHome,
        int monthlyRent,
        int monthlyLoanPayment,
        int monthlyDisposable,
        double disposableRatio,
        string sentimentTag,
        string? scenarioTitle)
    {
        Id = id;
        CreatedAt = createdAt;
        SchoolId = schoolId;
        SchoolName = schoolName;
        CipCode = cipCode;
        CredentialLevel = credentialLevel;
        CbsaCode = cbsaCode;
        LocationName = locationName;
        HousingType = housingType;
        SalaryMode = salaryMode;
        ReportedAnnualSalary = reportedAnnualSalary;
        MonthlyNetTakeHome = monthlyNetTakeHome;
        MonthlyRent = monthlyRent;
        MonthlyLoanPayment = monthlyLoanPayment;
        MonthlyDisposable = monthlyDisposable;
        DisposableRatio = disposableRatio;
        SentimentTag = sentimentTag;
        ScenarioTitle = scenarioTitle;
    }
}

/// <summary>
/// Response payload returned after successfully sharing a scenario.
/// </summary>
public record ShareScenarioResponse(
    string Id,
    string Message
);
