using GradCast.Api.Models;

namespace GradCast.Api.Services;

/// <summary>
/// Calculates estimated federal and state income taxes for a single filer.
/// Tax brackets and rates are loaded from versioned JSON configuration files
/// via the injected tax config provider, allowing annual updates without recompilation.
/// This is an approximation for PoC/planning purposes, not tax advice.
/// </summary>
public class TaxCalculationService : ITaxCalculationService
{
    private readonly TaxConfig _config;

    public TaxCalculationService(ITaxConfigProvider configProvider)
    {
        _config = configProvider.GetConfig();
    }

    public int TaxYear => _config.TaxYear;

    public NetPayResult Calculate(decimal grossAnnualSalary, string state)
    {
        // Federal taxable income (after standard deduction)
        var federalTaxableIncome = Math.Max(0, grossAnnualSalary - _config.StandardDeduction);
        var federalTax = CalculateFederalTax(federalTaxableIncome);

        // FICA (Social Security + Medicare)
        var socialSecurity = Math.Min(grossAnnualSalary, _config.SocialSecurityWageCap) * _config.SocialSecurityRate;
        var medicare = grossAnnualSalary * _config.MedicareRate;
        var ficaTotal = socialSecurity + medicare;

        // State tax (simplified flat effective rate on gross)
        var stateRate = _config.StateTaxRates.GetValueOrDefault(state.ToUpperInvariant(), 0.04m);
        var stateTax = grossAnnualSalary * stateRate;

        // Net annual and monthly
        var totalTax = federalTax + ficaTotal + stateTax;
        var netAnnual = grossAnnualSalary - totalTax;
        var grossMonthly = Math.Round(grossAnnualSalary / 12, 2);
        var netMonthly = Math.Round(netAnnual / 12, 2);

        return new NetPayResult(
            GrossAnnual: grossAnnualSalary,
            GrossMonthly: grossMonthly,
            FederalTaxAnnual: Math.Round(federalTax, 2),
            FederalTaxMonthly: Math.Round(federalTax / 12, 2),
            FicaAnnual: Math.Round(ficaTotal, 2),
            FicaMonthly: Math.Round(ficaTotal / 12, 2),
            StateTaxAnnual: Math.Round(stateTax, 2),
            StateTaxMonthly: Math.Round(stateTax / 12, 2),
            StateTaxRate: stateRate,
            NetAnnual: Math.Round(netAnnual, 2),
            NetMonthly: netMonthly,
            EffectiveTaxRate: grossAnnualSalary > 0 ? Math.Round(totalTax / grossAnnualSalary, 4) : 0
        );
    }

    private decimal CalculateFederalTax(decimal taxableIncome)
    {
        var tax = 0m;
        var previousBound = 0m;

        foreach (var bracket in _config.FederalBrackets)
        {
            if (taxableIncome <= previousBound) break;

            var taxableInBracket = Math.Min(taxableIncome, bracket.UpperBound) - previousBound;
            tax += taxableInBracket * bracket.Rate;
            previousBound = bracket.UpperBound;
        }

        return tax;
    }
}
