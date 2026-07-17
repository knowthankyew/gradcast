namespace GradCast.Api.Models;

public record HousingCostResult(
    string CbsaCode,
    string HousingType,
    int MonthlyRent,
    int FmrYear,
    int FullRent
);
