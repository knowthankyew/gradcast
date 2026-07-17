using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GradCast.Data.Entities;

[Table("schools")]
public class School
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [Column("city")]
    [MaxLength(200)]
    public string City { get; set; } = string.Empty;

    [Column("state")]
    [MaxLength(10)]
    public string State { get; set; } = string.Empty;

    [Column("school_url")]
    [MaxLength(500)]
    public string? SchoolUrl { get; set; }

    [Column("ownership")]
    public int Ownership { get; set; }

    public ICollection<SchoolYearData> YearData { get; set; } = new List<SchoolYearData>();
    public ICollection<Program> Programs { get; set; } = new List<Program>();
}
