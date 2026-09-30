namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

/// <summary>Da el nombre de un país en la cultura que consulta el catálogo.</summary>
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
