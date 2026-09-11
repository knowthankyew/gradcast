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
                $"No tax configuration files found in {taxDataDir}. Add a tax_config_YYYY.json file.");
        }

        var latestFile = configFiles[0];
        var json = File.ReadAllText(latestFile);
        using var doc = JsonDocument.Parse(json);
        var raw = doc.RootElement;

        var federalBrackets = new List<TaxBracket>();
        var bracketsProp = GetRequiredProperty(raw, "federalBrackets");
        if (bracketsProp.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Tax configuration property 'federalBrackets' must be an array.");
        }

        foreach (var bracket in bracketsProp.EnumerateArray())
        {
            federalBrackets.Add(new TaxBracket(
                GetRequiredProperty(bracket, "upperBound").GetDecimal(),
                GetRequiredProperty(bracket, "rate").GetDecimal()));
        }

        var stateTaxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var stateRatesProp = GetRequiredProperty(raw, "stateTaxRates");
        if (stateRatesProp.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Tax configuration property 'stateTaxRates' must be an object.");
        }

        foreach (var prop in stateRatesProp.EnumerateObject())
        {
            stateTaxRates[prop.Name.ToUpperInvariant()] = prop.Value.GetDecimal();
        }

        return new TaxConfig
        {
            TaxYear = GetRequiredProperty(raw, "taxYear").GetInt32(),
            StandardDeduction = GetRequiredProperty(raw, "standardDeduction").GetDecimal(),
            SocialSecurityRate = GetRequiredProperty(raw, "socialSecurityRate").GetDecimal(),
            SocialSecurityWageCap = GetRequiredProperty(raw, "socialSecurityWageCap").GetDecimal(),
            MedicareRate = GetRequiredProperty(raw, "medicareRate").GetDecimal(),
            FederalBrackets = federalBrackets,
            StateTaxRates = stateTaxRates
        };
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            throw new InvalidOperationException($"Tax configuration is missing required property: '{propertyName}'.");
        }
        return prop;
    }
}
