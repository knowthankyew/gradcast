namespace GradCast.Api.Models;

public record LocationSearchResult(
    string CbsaCode,
    string Name,
    string State,
    string Type
);
