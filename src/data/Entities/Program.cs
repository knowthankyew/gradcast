using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GradCast.Data.Entities;

/// <summary>
/// Program (field of study) data at the CIP 4-digit level for a school + year.
/// </summary>
[Table("programs")]
public class Program
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("school_id")]
    public int SchoolId { get; set; }

    [Column("year")]
    public int Year { get; set; }

    [Column("cip_code")]
    [MaxLength(10)]
    public string CipCode { get; set; } = string.Empty;

    [Column("title")]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Column("credential_level")]
    public int CredentialLevel { get; set; }

    [Column("completions")]
    public int? Completions { get; set; }

    [Column("median_earnings")]
    public decimal? MedianEarnings { get; set; }

    [ForeignKey(nameof(SchoolId))]
    public School School { get; set; } = null!;
}
