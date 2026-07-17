using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GradCast.Data.Entities;

/// <summary>
/// HUD Fair Market Rent data by CBSA metro area.
/// Source: HUD User portal (huduser.gov), published annually.
/// Stores rent estimates at the 40th percentile by bedroom count.
/// </summary>
[Table("fair_market_rents")]
public class FairMarketRent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("cbsa_code")]
    [MaxLength(10)]
    public string CbsaCode { get; set; } = string.Empty;

    [Column("year")]
    public int Year { get; set; }

    [Column("efficiency")]
    public int Efficiency { get; set; }

    [Column("one_bedroom")]
    public int OneBedroom { get; set; }

    [Column("two_bedroom")]
    public int TwoBedroom { get; set; }

    [Column("three_bedroom")]
    public int ThreeBedroom { get; set; }

    [Column("four_bedroom")]
    public int FourBedroom { get; set; }
}
