namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

public sealed class CurrencyTranslation
{
    private CurrencyTranslation() { }

    public string CurrencyCode { get; private set; } = string.Empty;
    public string Culture { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string NamePlural { get; private set; } = string.Empty;
    public string DisplaySymbol { get; private set; } = string.Empty;

    public static CurrencyTranslation Create(string currencyCode, string culture, string name,
        string namePlural, string displaySymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(namePlural);
        ArgumentException.ThrowIfNullOrWhiteSpace(displaySymbol);

        return new CurrencyTranslation
        {
            CurrencyCode = currencyCode,
            Culture = culture,
            Name = name,
            NamePlural = namePlural,
            DisplaySymbol = displaySymbol,
        };
    }
}
