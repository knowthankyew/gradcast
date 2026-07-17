using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GradCast.Data.Entities;

/// <summary>
/// Maps a county (FIPS code) to its parent CBSA.
/// This is the bridge between CBSA metro areas and HUD FMR data (keyed by FIPS).
/// </summary>
[Table("cbsa_counties")]
public class CbsaCounty
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("cbsa_code")]
    [MaxLength(10)]
    public string CbsaCode { get; set; } = string.Empty;

    [Column("fips_state")]
    [MaxLength(2)]
    public string FipsState { get; set; } = string.Empty;

    [Column("fips_county")]
    [MaxLength(3)]
    public string FipsCounty { get; set; } = string.Empty;

    /// <summary>
    /// Full 5-digit FIPS code (state + county). Used for HUD FMR lookups.
    /// </summary>
    [Column("fips_code")]
    [MaxLength(5)]
    public string FipsCode { get; set; } = string.Empty;

    [Column("county_name")]
    [MaxLength(200)]
    public string CountyName { get; set; } = string.Empty;

    [Column("state_abbr")]
    [MaxLength(2)]
    public string StateAbbr { get; set; } = string.Empty;

    [ForeignKey(nameof(CbsaCode))]
    public CbsaLocation CbsaLocation { get; set; } = null!;
}
