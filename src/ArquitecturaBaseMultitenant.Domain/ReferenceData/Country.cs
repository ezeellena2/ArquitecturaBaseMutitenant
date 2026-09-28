namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

public sealed class Country
{
    private Country() { }

    public string Code { get; private set; } = string.Empty;
    public string Alpha3 { get; private set; } = string.Empty;
    public string NumericCode { get; private set; } = string.Empty;
    public string? CallingCode { get; private set; }
    public string? DefaultCurrencyCode { get; private set; }
    public string? DefaultTimeZoneId { get; private set; }
    public bool IsEnabled { get; private set; }
    public int? SortOrder { get; private set; }

    public static Country Create(string code, string alpha3, string numericCode, string? callingCode,
        string? defaultCurrencyCode, string? defaultTimeZoneId, bool isEnabled, int? sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(alpha3);
        ArgumentException.ThrowIfNullOrWhiteSpace(numericCode);

        return new Country
        {
            Code = code,
            Alpha3 = alpha3,
            NumericCode = numericCode,
            CallingCode = callingCode,
            DefaultCurrencyCode = defaultCurrencyCode,
            DefaultTimeZoneId = defaultTimeZoneId,
            IsEnabled = isEnabled,
            SortOrder = sortOrder,
        };
    }
}
