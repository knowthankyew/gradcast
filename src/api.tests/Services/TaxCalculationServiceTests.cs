using GradCast.Api.Services;

namespace GradCast.Api.Tests;

public class TaxCalculationServiceTests
{
    [Fact]
    public void ConstructorAcceptsProviderAndUsesReturnedConfig()
    {
        var provider = new StubTaxConfigProvider(new TaxConfig
        {
            TaxYear = 2026,
            StandardDeduction = 0,
            SocialSecurityRate = 0.062m,
            SocialSecurityWageCap = 176100,
            MedicareRate = 0.0145m,
            FederalBrackets = new[]
            {
                new TaxBracket(100000, 0.10m)
            },
            StateTaxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["CA"] = 0.06m
            }
        });

        var service = new TaxCalculationService(provider);

        Assert.Equal(2026, service.TaxYear);

        var result = service.Calculate(75000m, "ca");
        Assert.Equal(75000m, result.GrossAnnual);
        Assert.Equal(6250.00m, result.GrossMonthly);
        Assert.Equal(0.06m, result.StateTaxRate);
        Assert.Equal(7500.00m, result.FederalTaxAnnual);
        Assert.Equal(625.00m, result.FederalTaxMonthly);
        Assert.Equal(4500.00m, result.StateTaxAnnual);
        Assert.Equal(375.00m, result.StateTaxMonthly);
        Assert.Equal(5737.50m, result.FicaAnnual);
        Assert.Equal(478.12m, result.FicaMonthly);
        Assert.Equal(57262.50m, result.NetAnnual);
        Assert.Equal(4771.88m, result.NetMonthly);
        Assert.Equal(0.2365m, result.EffectiveTaxRate);
    }

    [Fact]
    public void EmptyFederalBracketCollectionReturnsZeroFederalTax()
    {
        var provider = new StubTaxConfigProvider(new TaxConfig
        {
            TaxYear = 2026,
            StandardDeduction = 0,
            SocialSecurityRate = 0.062m,
            SocialSecurityWageCap = 176100,
            MedicareRate = 0.0145m,
            FederalBrackets = Array.Empty<TaxBracket>(),
            StateTaxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["CA"] = 0.06m
            }
        });

        var service = new TaxCalculationService(provider);
        var result = service.Calculate(10000m, "CA");

        Assert.Equal(0m, result.FederalTaxAnnual);
    }

    [Fact]
    public void UnknownStateUsesFourPercentFallbackOnGrossSalary()
    {
        var provider = new StubTaxConfigProvider(new TaxConfig
        {
            TaxYear = 2026,
            StandardDeduction = 0,
            SocialSecurityRate = 0.062m,
            SocialSecurityWageCap = 176100,
            MedicareRate = 0.0145m,
            FederalBrackets = Array.Empty<TaxBracket>(),
            StateTaxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["CA"] = 0.06m
            }
        });

        var service = new TaxCalculationService(provider);
        var result = service.Calculate(100000m, "ZZ");

        var expected = Math.Round(100000m * 0.04m, 2);
        Assert.Equal(expected, result.StateTaxAnnual);
    }
}
