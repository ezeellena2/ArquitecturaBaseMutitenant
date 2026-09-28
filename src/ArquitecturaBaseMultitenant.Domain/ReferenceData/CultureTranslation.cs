namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

public sealed class CultureTranslation
{
    private CultureTranslation() { }

    public string CultureCode { get; private set; } = string.Empty;
    public string DisplayCulture { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    public static CultureTranslation Create(string cultureCode, string displayCulture, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayCulture);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new CultureTranslation { CultureCode = cultureCode, DisplayCulture = displayCulture, Name = name };
    }
}
