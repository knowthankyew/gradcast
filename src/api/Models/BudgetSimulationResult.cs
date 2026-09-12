namespace GradCast.Api.Models;

public record BudgetSimulationResult(
    // Income
    decimal GrossAnnualSalary,
    decimal GrossMonthly,
    decimal NetMonthly,
    decimal EffectiveTaxRate,
    string SalarySource,

    // Fixed costs
    decimal RentMonthly,
    string HousingType,
    decimal LoanPaymentMonthly,
    decimal LoanPrincipal,

    // Bottom line
    decimal FixedCostsMonthly,
    decimal DisposableMonthly,
    string IncomeStatus,

    // Context
    string LocationName,
    string State,

    // Metadata
    bool HasReportedEarnings = true
);
