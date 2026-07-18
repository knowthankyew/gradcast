using System.Text.Json;
using GradCast.Api.Models;

namespace GradCast.Api.Services;

/// <summary>
/// Calculates estimated federal and state income taxes for a single filer.
/// Tax brackets and rates are loaded from versioned JSON configuration files
/// (src/api/Configuration/TaxData/tax_config_{year}.json), allowing annual
/// updates without recompilation.
/// This is an approximation for PoC/planning purposes, not tax advice.
/// </summary>
public class TaxCalculationService
{
    private readonly TaxConfig _config;

    public TaxCalculationService(IWebHostEnvironment env)
    {
        _config = LoadConfig(env.ContentRootPath);
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

    private static TaxConfig LoadConfig(string contentRootPath)
    {
        // Find the most recent tax config file
        var taxDataDir = Path.Combine(contentRootPath, "Configuration", "TaxData");

        if (!Directory.Exists(taxDataDir))
        {
            throw new InvalidOperationException(
                $"Tax configuration directory not found: {taxDataDir}. " +
                "Ensure Configuration/TaxData/tax_config_YYYY.json exists.");
        }

        var configFiles = Directory.GetFiles(taxDataDir, "tax_config_*.json")
            .OrderByDescending(f => f)
            .ToList();

        if (configFiles.Count == 0)
        {
            throw new InvalidOperationException(
                "No tax configuration files found. Add a tax_config_YYYY.json file.");
        }

        var latestFile = configFiles[0];
        var json = File.ReadAllText(latestFile);
        var raw = JsonDocument.Parse(json).RootElement;

        var config = new TaxConfig
        {
            TaxYear = raw.GetProperty("taxYear").GetInt32(),
            StandardDeduction = raw.GetProperty("standardDeduction").GetDecimal(),
            SocialSecurityRate = raw.GetProperty("socialSecurityRate").GetDecimal(),
            SocialSecurityWageCap = raw.GetProperty("socialSecurityWageCap").GetDecimal(),
            MedicareRate = raw.GetProperty("medicareRate").GetDecimal(),
        };

        foreach (var bracket in raw.GetProperty("federalBrackets").EnumerateArray())
        {
            config.FederalBrackets.Add(new TaxBracket(
                bracket.GetProperty("upperBound").GetDecimal(),
                bracket.GetProperty("rate").GetDecimal()
            ));
        }

        foreach (var prop in raw.GetProperty("stateTaxRates").EnumerateObject())
        {
            config.StateTaxRates[prop.Name.ToUpperInvariant()] = prop.Value.GetDecimal();
        }

        return config;
    }

    private record TaxBracket(decimal UpperBound, decimal Rate);

    private class TaxConfig
    {
        public int TaxYear { get; set; }
        public decimal StandardDeduction { get; set; }
        public decimal SocialSecurityRate { get; set; }
        public decimal SocialSecurityWageCap { get; set; }
        public decimal MedicareRate { get; set; }
        public List<TaxBracket> FederalBrackets { get; set; } = new();
        public Dictionary<string, decimal> StateTaxRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
