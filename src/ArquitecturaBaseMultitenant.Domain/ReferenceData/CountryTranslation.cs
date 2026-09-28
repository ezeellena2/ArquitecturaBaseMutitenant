namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

public sealed class CountryTranslation
{
    private CountryTranslation() { }

    public string CountryCode { get; private set; } = string.Empty;
    public string Culture { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    public static CountryTranslation Create(string countryCode, string culture, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new CountryTranslation { CountryCode = countryCode, Culture = culture, Name = name };
    }
}
