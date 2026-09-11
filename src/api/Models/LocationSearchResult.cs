namespace GradCast.Api.Models;

public record LocationSearchResult(
    string CbsaCode,
    string Name,
    string State,
    string Type,
    int? OneBedRent = null,
    int? TwoBedRent = null,
    bool HasHousingData = true
);
