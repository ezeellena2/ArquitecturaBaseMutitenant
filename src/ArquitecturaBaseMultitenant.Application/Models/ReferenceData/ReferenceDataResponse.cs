namespace ArquitecturaBaseMultitenant.Application.Models.ReferenceData;

public sealed record ReferenceDataResponse(
    string Culture,
    IReadOnlyList<CurrencyReferenceItem> Currencies,
    IReadOnlyList<CountryReferenceItem> Countries,
    IReadOnlyList<TimeZoneReferenceItem> TimeZones,
    IReadOnlyList<CultureReferenceItem> Cultures,
    IReadOnlyList<TaxIdTypeReferenceItem> TaxIdTypes);

public sealed record CurrencyReferenceItem(
    string Code, string NumericCode, int? MinorUnits, string Symbol,
    string DisplaySymbol, string Name, string NamePlural, bool IsEnabled, int? SortOrder);

public sealed record CountryReferenceItem(
    string Code, string Alpha3, string NumericCode, string? CallingCode,
    string? DefaultCurrencyCode, string? DefaultTimeZoneId, string Name,
    bool IsEnabled, int? SortOrder);

public sealed record TimeZoneReferenceItem(
    string Id, IReadOnlyList<string> CountryCodes, string City, bool IsEnabled, int? SortOrder);

public sealed record CultureReferenceItem(
    string Code, string LanguageCode, string CountryCode,
    string DatePattern, string TimePattern, string DateTimePattern, string LongDatePattern,
    string DecimalSeparator, string GroupSeparator, string CurrencyPattern,
    string PercentPattern, string? FallbackCulture, bool IsDefault, string Name,
    bool IsEnabled, int? SortOrder);

public sealed record TaxIdTypeReferenceItem(
    string Code, string CountryCode, string Label, string Mask, string ValidatorKey,
    string AppliesTo, string Name, bool IsEnabled, int? SortOrder);
