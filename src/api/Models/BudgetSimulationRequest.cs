namespace GradCast.Api.Models;

public record BudgetSimulationRequest(
    int SchoolId,
    string? CipCode,
    string CbsaCode,
    string HousingType,
    decimal? SalaryOverride,
    int? CredentialLevel = null
);
