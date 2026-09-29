namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

public sealed class Culture
{
    private Culture() { }

    public string Code { get; private set; } = string.Empty;
    public string LanguageCode { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;
    public string DatePattern { get; private set; } = string.Empty;
    public string TimePattern { get; private set; } = string.Empty;
    public string DateTimePattern { get; private set; } = string.Empty;
    public string LongDatePattern { get; private set; } = string.Empty;
    public string AmDesignator { get; private set; } = string.Empty;
    public string PmDesignator { get; private set; } = string.Empty;
    public string DecimalSeparator { get; private set; } = string.Empty;
    public string GroupSeparator { get; private set; } = string.Empty;
    public string CurrencyPattern { get; private set; } = string.Empty;
    public string PercentPattern { get; private set; } = string.Empty;
    public string? FallbackCulture { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsDefault { get; private set; }
    public int? SortOrder { get; private set; }

    public static Culture Create(string code, string languageCode, string countryCode,
        string datePattern, string timePattern, string dateTimePattern, string longDatePattern,
        string amDesignator, string pmDesignator,
        string decimalSeparator, string groupSeparator, string currencyPattern, string percentPattern,
        string? fallbackCulture, bool isEnabled, bool isDefault, int? sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(datePattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(timePattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateTimePattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(longDatePattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(amDesignator);
        ArgumentException.ThrowIfNullOrWhiteSpace(pmDesignator);
        ArgumentException.ThrowIfNullOrWhiteSpace(decimalSeparator);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupSeparator);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyPattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(percentPattern);

        return new Culture
        {
            Code = code,
            LanguageCode = languageCode,
            CountryCode = countryCode,
            DatePattern = datePattern,
            TimePattern = timePattern,
            DateTimePattern = dateTimePattern,
            LongDatePattern = longDatePattern,
            AmDesignator = amDesignator,
            PmDesignator = pmDesignator,
            DecimalSeparator = decimalSeparator,
            GroupSeparator = groupSeparator,
            CurrencyPattern = currencyPattern,
            PercentPattern = percentPattern,
            FallbackCulture = fallbackCulture,
            IsEnabled = isEnabled,
            IsDefault = isDefault,
            SortOrder = sortOrder,
        };
    }
}
