namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

/// <summary>Da el nombre localizado de un tipo de identificación fiscal sin cambiar su clave.</summary>
public sealed class TaxIdTypeTranslation
{
    private TaxIdTypeTranslation() { }

    public string TaxIdTypeCode { get; private set; } = string.Empty;
    public string Culture { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    public static TaxIdTypeTranslation Create(string taxIdTypeCode, string culture, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taxIdTypeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new TaxIdTypeTranslation { TaxIdTypeCode = taxIdTypeCode, Culture = culture, Name = name };
    }
}
