using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GradCast.Data.Entities;

[Table("community_scenario_benchmarks")]
public class CommunityScenarioBenchmark
{
    [Column("cip_family")]
    [MaxLength(10)]
    public string CipFamily { get; set; } = string.Empty;

    [Column("cbsa_code")]
    [MaxLength(10)]
    public string CbsaCode { get; set; } = string.Empty;

    [Column("credential_level")]
    public int CredentialLevel { get; set; }

    [Column("housing_type")]
    [MaxLength(50)]
    public string HousingType { get; set; } = string.Empty;

    [Column("sample_count")]
    public int SampleCount { get; set; }

    [Column("p25_monthly_disposable")]
    public int P25MonthlyDisposable { get; set; }

    [Column("median_monthly_disposable")]
    public int MedianMonthlyDisposable { get; set; }

    [Column("p75_monthly_disposable")]
    public int P75MonthlyDisposable { get; set; }

    [Column("top_contributing_school_id")]
    public int? TopContributingSchoolId { get; set; }

    [Column("last_updated_at")]
    public string LastUpdatedAt { get; set; } = string.Empty;
}
