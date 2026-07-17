namespace GradCast.Api.Models;

public record NetPayResult(
    decimal GrossAnnual,
    decimal GrossMonthly,
    decimal FederalTaxAnnual,
    decimal FederalTaxMonthly,
    decimal FicaAnnual,
    decimal FicaMonthly,
    decimal StateTaxAnnual,
    decimal StateTaxMonthly,
    decimal StateTaxRate,
    decimal NetAnnual,
    decimal NetMonthly,
    decimal EffectiveTaxRate
);
