using System.Text.Json;
using GradCast.Api.Services;

namespace GradCast.Api.Tests;

public class FileTaxConfigProviderTests
{
    [Fact]
    public void FileTaxConfigProviderThrowsWhenTaxDirectoryIsMissing()
    {
        var env = new StubWebHostEnvironment(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var provider = new FileTaxConfigProvider(env);

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetConfig());
        Assert.Contains("Configuration/TaxData", ex.Message);
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void FileTaxConfigProviderThrowsWhenDirectoryExistsButHasNoTaxFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Configuration", "TaxData"));

        var env = new StubWebHostEnvironment(root);
        var provider = new FileTaxConfigProvider(env);

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetConfig());
        Assert.Contains("No tax configuration files found", ex.Message);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void FileTaxConfigProviderThrowsWhenJsonIsMalformed()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "Configuration", "TaxData");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "tax_config_2026.json"), "{ invalid-json-syntax }");

        var env = new StubWebHostEnvironment(root);
        var provider = new FileTaxConfigProvider(env);

        try
        {
            Assert.ThrowsAny<JsonException>(() => provider.GetConfig());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("taxYear")]
    [InlineData("standardDeduction")]
    [InlineData("socialSecurityRate")]
    [InlineData("socialSecurityWageCap")]
    [InlineData("medicareRate")]
    [InlineData("federalBrackets")]
    [InlineData("stateTaxRates")]
    public void FileTaxConfigProviderThrowsWhenRequiredFieldIsMissing(string missingField)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "Configuration", "TaxData");
        Directory.CreateDirectory(dir);

        var dict = new Dictionary<string, object>
        {
            ["taxYear"] = 2026,
            ["standardDeduction"] = 15000,
            ["socialSecurityRate"] = 0.062,
            ["socialSecurityWageCap"] = 176100,
            ["medicareRate"] = 0.0145,
            ["federalBrackets"] = Array.Empty<object>(),
            ["stateTaxRates"] = new Dictionary<string, decimal> { ["CA"] = 0.06m }
        };
        dict.Remove(missingField);

        File.WriteAllText(Path.Combine(dir, "tax_config_2026.json"), JsonSerializer.Serialize(dict));

        var env = new StubWebHostEnvironment(root);
        var provider = new FileTaxConfigProvider(env);

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => provider.GetConfig());
            Assert.Contains(missingField, ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FileTaxConfigProviderSelectsLatestLexicographicalConfigFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "Configuration", "TaxData");
        Directory.CreateDirectory(dir);

        var config2025 = new
        {
            taxYear = 2025,
            standardDeduction = 14600,
            socialSecurityRate = 0.062,
            socialSecurityWageCap = 168600,
            medicareRate = 0.0145,
            federalBrackets = Array.Empty<object>(),
            stateTaxRates = new Dictionary<string, decimal> { ["CA"] = 0.05m }
        };
        var config2026 = new
        {
            taxYear = 2026,
            standardDeduction = 15000,
            socialSecurityRate = 0.062,
            socialSecurityWageCap = 176100,
            medicareRate = 0.0145,
            federalBrackets = Array.Empty<object>(),
            stateTaxRates = new Dictionary<string, decimal> { ["CA"] = 0.06m }
        };

        File.WriteAllText(Path.Combine(dir, "tax_config_2025.json"), JsonSerializer.Serialize(config2025));
        File.WriteAllText(Path.Combine(dir, "tax_config_2026.json"), JsonSerializer.Serialize(config2026));

        var env = new StubWebHostEnvironment(root);
        var provider = new FileTaxConfigProvider(env);

        try
        {
            var config = provider.GetConfig();
            Assert.Equal(2026, config.TaxYear);
            Assert.Equal(15000m, config.StandardDeduction);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
