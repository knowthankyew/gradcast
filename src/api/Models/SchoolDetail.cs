namespace GradCast.Api.Models;

public record SchoolDetail(
    int Id,
    string Name,
    string City,
    string State,
    string? SchoolUrl,
    int Ownership,
    string OwnershipName,
    decimal? AdmissionRate,
    int? StudentSize,
    int? TuitionInState,
    int? TuitionOutOfState,
    decimal? CompletionRate,
    IReadOnlyList<ProgramData> Programs
);
