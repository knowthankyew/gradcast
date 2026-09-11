namespace GradCast.Api.Services;

public record TaxBracket(decimal UpperBound, decimal Rate);

public record TaxConfig
{
    public int TaxYear { get; init; }
    public decimal StandardDeduction { get; init; }
    public decimal SocialSecurityRate { get; init; }
    public decimal SocialSecurityWageCap { get; init; }
    public decimal MedicareRate { get; init; }
    public IReadOnlyList<TaxBracket> FederalBrackets { get; init; } = Array.Empty<TaxBracket>();
    public IReadOnlyDictionary<string, decimal> StateTaxRates { get; init; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
}
