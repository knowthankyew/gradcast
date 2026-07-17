namespace GradCast.Api.Models;

public record ProgramData(
    string Code,
    string Title,
    int CredentialLevel,
    string CredentialName,
    int? Completions,
    decimal? MedianEarnings
);
