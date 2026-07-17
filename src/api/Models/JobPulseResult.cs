namespace GradCast.Api.Models;

public record JobPulseResult(
    string CipCode,
    string CbsaCode,
    string SearchKeywords,
    int ActiveOpenings,
    decimal? LocalMedianSalary,
    decimal? ScorecardMedianEarnings,
    string DataSource,
    string LocationName
);
