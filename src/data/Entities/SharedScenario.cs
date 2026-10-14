using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GradCast.Data.Entities;

[Table("shared_scenarios")]
public class SharedScenario
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [Column("school_id")]
    public int SchoolId { get; set; }

    [Column("cip_code")]
    [MaxLength(20)]
    public string CipCode { get; set; } = string.Empty;

    [Column("credential_level")]
    public int CredentialLevel { get; set; }

    [Column("cbsa_code")]
    [MaxLength(10)]
    public string CbsaCode { get; set; } = string.Empty;

    [Column("housing_type")]
    [MaxLength(50)]
    public string HousingType { get; set; } = string.Empty;

    [Column("salary_mode")]
    [MaxLength(50)]
    public string SalaryMode { get; set; } = string.Empty;

    [Column("reported_annual_salary")]
    public int ReportedAnnualSalary { get; set; }

    [Column("monthly_net_take_home")]
    public int MonthlyNetTakeHome { get; set; }

    [Column("monthly_rent")]
    public int MonthlyRent { get; set; }

    [Column("monthly_loan_payment")]
    public int MonthlyLoanPayment { get; set; }

    [Column("monthly_disposable")]
    public int MonthlyDisposable { get; set; }

    [Column("disposable_ratio")]
    public double DisposableRatio { get; set; }

    [Column("sentiment_tag")]
    [MaxLength(50)]
    public string SentimentTag { get; set; } = string.Empty;

    [Column("scenario_title")]
    [MaxLength(60)]
    public string? ScenarioTitle { get; set; }

    [Column("client_hash")]
    [MaxLength(64)]
    public string ClientHash { get; set; } = string.Empty;
}
