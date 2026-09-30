namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

/// <summary>Conserva el código ISO y las unidades menores que se usan para validar y redondear importes.</summary>
public sealed class Currency
{
    private Currency() { }

    public string Code { get; private set; } = string.Empty;
    public string NumericCode { get; private set; } = string.Empty;
    public int? MinorUnits { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public int? SortOrder { get; private set; }

    public static Currency Create(string code, string numericCode, int? minorUnits, string symbol,
        bool isEnabled, int? sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(numericCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (minorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minorUnits));
        }

        return new Currency
        {
            Code = code,
            NumericCode = numericCode,
            MinorUnits = minorUnits,
            Symbol = symbol,
            IsEnabled = isEnabled,
            SortOrder = sortOrder,
        };
    }
}
