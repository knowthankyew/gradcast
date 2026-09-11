using System.Text.Json;

namespace GradCast.Api.Services;

public class FileTaxConfigProvider : ITaxConfigProvider
{
    private readonly IWebHostEnvironment _env;

    public FileTaxConfigProvider(IWebHostEnvironment env)
    {
        _env = env;
    }

    public TaxConfig GetConfig()
    {
        var taxDataDir = Path.Combine(_env.ContentRootPath, "Configuration", "TaxData");

        if (!Directory.Exists(taxDataDir))
        {
            throw new InvalidOperationException(
                $"Tax configuration directory not found: {taxDataDir}. Ensure Configuration/TaxData/tax_config_YYYY.json exists.");
        }

        var configFiles = Directory.GetFiles(taxDataDir, "tax_config_*.json")
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (configFiles.Count == 0)
        {
            throw new InvalidOperationException(
                "No tax configuration files found. Add a tax_config_YYYY.json file.");
        }

        var latestFile = configFiles[0];
        var json = File.ReadAllText(latestFile);
        var raw = JsonDocument.Parse(json).RootElement;

        var federalBrackets = new List<TaxBracket>();
        foreach (var bracket in raw.GetProperty("federalBrackets").EnumerateArray())
        {
            federalBrackets.Add(new TaxBracket(
                bracket.GetProperty("upperBound").GetDecimal(),
                bracket.GetProperty("rate").GetDecimal()));
        }

        var stateTaxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in raw.GetProperty("stateTaxRates").EnumerateObject())
        {
            stateTaxRates[prop.Name.ToUpperInvariant()] = prop.Value.GetDecimal();
        }

        return new TaxConfig
        {
            TaxYear = raw.GetProperty("taxYear").GetInt32(),
            StandardDeduction = raw.GetProperty("standardDeduction").GetDecimal(),
            SocialSecurityRate = raw.GetProperty("socialSecurityRate").GetDecimal(),
            SocialSecurityWageCap = raw.GetProperty("socialSecurityWageCap").GetDecimal(),
            MedicareRate = raw.GetProperty("medicareRate").GetDecimal(),
            FederalBrackets = federalBrackets,
            StateTaxRates = stateTaxRates
        };
    }
}
