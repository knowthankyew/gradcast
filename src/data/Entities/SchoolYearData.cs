using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GradCast.Data.Entities;

/// <summary>
/// Year-specific metrics for a school (tuition, enrollment, admission rate, completion rate).
/// One row per school per year.
/// </summary>
[Table("school_year_data")]
public class SchoolYearData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("school_id")]
    public int SchoolId { get; set; }

    [Column("year")]
    public int Year { get; set; }

    [Column("admission_rate")]
    public decimal? AdmissionRate { get; set; }

    [Column("student_size")]
    public int? StudentSize { get; set; }

    [Column("tuition_in_state")]
    public int? TuitionInState { get; set; }

    [Column("tuition_out_of_state")]
    public int? TuitionOutOfState { get; set; }

    [Column("completion_rate")]
    public decimal? CompletionRate { get; set; }

    [ForeignKey(nameof(SchoolId))]
    public School School { get; set; } = null!;
}
