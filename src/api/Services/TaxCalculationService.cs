using GradCast.Api.Models;

namespace GradCast.Api.Services;

/// <summary>
/// Calculates estimated federal and state income taxes for a single filer.
/// Uses 2024 tax brackets and standard deduction. Zero external dependencies.
/// This is an approximation for PoC/planning purposes, not tax advice.
/// </summary>
public class TaxCalculationService
{
    // 2024 federal tax brackets (single filer)
    private static readonly (decimal UpperBound, decimal Rate)[] FederalBrackets =
    [
        (11_600m, 0.10m),
        (47_150m, 0.12m),
        (100_525m, 0.22m),
        (191_950m, 0.24m),
        (243_725m, 0.32m),
        (609_350m, 0.35m),
        (decimal.MaxValue, 0.37m),
    ];

    private const decimal StandardDeduction2024 = 14_600m;
    private const decimal SocialSecurityRate = 0.062m;
    private const decimal SocialSecurityWageCap = 168_600m;
    private const decimal MedicareRate = 0.0145m;

    // Simplified state income tax rates (effective rates for a typical new grad salary)
    // States with no income tax get 0. Progressive states use a simplified flat effective rate.
    private static readonly Dictionary<string, decimal> StateTaxRates = new(StringComparer.OrdinalIgnoreCase)
    {
        // No income tax states
        ["AK"] = 0m, ["FL"] = 0m, ["NV"] = 0m, ["NH"] = 0m,
        ["SD"] = 0m, ["TN"] = 0m, ["TX"] = 0m, ["WA"] = 0m, ["WY"] = 0m,

        // Flat tax states (exact rates)
        ["CO"] = 0.044m, ["IL"] = 0.0495m, ["IN"] = 0.0305m,
        ["KY"] = 0.04m, ["MA"] = 0.05m, ["MI"] = 0.0425m,
        ["NC"] = 0.0475m, ["PA"] = 0.0307m, ["UT"] = 0.0465m,

        // Progressive states (simplified effective rate for ~$50-80k income)
        ["AL"] = 0.04m, ["AZ"] = 0.035m, ["AR"] = 0.044m,
        ["CA"] = 0.06m, ["CT"] = 0.05m, ["DE"] = 0.05m,
        ["GA"] = 0.0475m, ["HI"] = 0.065m, ["ID"] = 0.058m,
        ["IA"] = 0.044m, ["KS"] = 0.046m, ["LA"] = 0.035m,
        ["ME"] = 0.055m, ["MD"] = 0.05m, ["MN"] = 0.055m,
        ["MS"] = 0.04m, ["MO"] = 0.048m, ["MT"] = 0.05m,
        ["NE"] = 0.05m, ["NJ"] = 0.04m, ["NM"] = 0.04m,
        ["NY"] = 0.055m, ["ND"] = 0.02m, ["OH"] = 0.035m,
        ["OK"] = 0.04m, ["OR"] = 0.075m, ["RI"] = 0.045m,
        ["SC"] = 0.05m, ["VT"] = 0.055m, ["VA"] = 0.0475m,
        ["WV"] = 0.045m, ["WI"] = 0.05m,

        // Territories
        ["DC"] = 0.065m, ["PR"] = 0.04m, ["VI"] = 0.035m,
        ["GU"] = 0.04m, ["AS"] = 0.03m, ["MP"] = 0.03m,
    };

    public NetPayResult Calculate(decimal grossAnnualSalary, string state)
    {
        // Federal taxable income (after standard deduction)
        var federalTaxableIncome = Math.Max(0, grossAnnualSalary - StandardDeduction2024);
        var federalTax = CalculateFederalTax(federalTaxableIncome);

        // FICA (Social Security + Medicare)
        var socialSecurity = Math.Min(grossAnnualSalary, SocialSecurityWageCap) * SocialSecurityRate;
        var medicare = grossAnnualSalary * MedicareRate;
        var ficaTotal = socialSecurity + medicare;

        // State tax (simplified flat effective rate on gross)
        var stateRate = StateTaxRates.GetValueOrDefault(state, 0.04m); // Default 4% if unknown
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

    private static decimal CalculateFederalTax(decimal taxableIncome)
    {
        var tax = 0m;
        var previousBound = 0m;

        foreach (var (upperBound, rate) in FederalBrackets)
        {
            if (taxableIncome <= previousBound) break;

            var taxableInBracket = Math.Min(taxableIncome, upperBound) - previousBound;
            tax += taxableInBracket * rate;
            previousBound = upperBound;
        }

        return tax;
    }
}
