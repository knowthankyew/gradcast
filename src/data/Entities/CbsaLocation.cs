using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GradCast.Data.Entities;

/// <summary>
/// Core-Based Statistical Area (CBSA) — represents a US metro or micro area.
/// Maps to HUD Fair Market Rent data via constituent county FIPS codes.
/// Source: Census Bureau delineation files.
/// </summary>
[Table("cbsa_locations")]
public class CbsaLocation
{
    [Key]
    [Column("cbsa_code")]
    [MaxLength(10)]
    public string CbsaCode { get; set; } = string.Empty;

    [Column("name")]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [Column("state")]
    [MaxLength(10)]
    public string State { get; set; } = string.Empty;

    [Column("type")]
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty; // "Metropolitan" or "Micropolitan"

    public ICollection<CbsaCounty> Counties { get; set; } = new List<CbsaCounty>();
}
