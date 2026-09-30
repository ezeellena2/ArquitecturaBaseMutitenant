namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

/// <summary>Describe el tipo fiscal de un país con su máscara y la clave del validador, sin validar el número aquí.</summary>
public sealed class TaxIdType
{
    private TaxIdType() { }

    public string Code { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string Mask { get; private set; } = string.Empty;
    public string ValidatorKey { get; private set; } = string.Empty;
    public string AppliesTo { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public int? SortOrder { get; private set; }

    public static TaxIdType Create(string code, string countryCode, string label, string mask,
        string validatorKey, string appliesTo, bool isEnabled, int? sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(mask);
        ArgumentException.ThrowIfNullOrWhiteSpace(validatorKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(appliesTo);

        return new TaxIdType
        {
            Code = code,
            CountryCode = countryCode,
            Label = label,
            Mask = mask,
            ValidatorKey = validatorKey,
            AppliesTo = appliesTo,
            IsEnabled = isEnabled,
            SortOrder = sortOrder,
        };
    }
}
