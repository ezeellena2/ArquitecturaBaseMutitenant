using ArquitecturaBaseMultitenant.Application.Models.ReferenceData;

namespace ArquitecturaBaseMultitenant.Api.Contracts.ReferenceData;

public sealed record ReferenceDataHttpResponse(
    string Culture,
    IReadOnlyList<CurrencyReferenceHttpResponse> Currencies,
    IReadOnlyList<CountryReferenceHttpResponse> Countries,
    IReadOnlyList<TimeZoneReferenceHttpResponse> TimeZones,
    IReadOnlyList<CultureReferenceHttpResponse> Cultures,
    IReadOnlyList<TaxIdTypeReferenceHttpResponse> TaxIdTypes)
{
    public static ReferenceDataHttpResponse FromModel(ReferenceDataResponse model) => new(
        model.Culture,
        model.Currencies.Select(CurrencyReferenceHttpResponse.FromModel).ToArray(),
        model.Countries.Select(CountryReferenceHttpResponse.FromModel).ToArray(),
        model.TimeZones.Select(TimeZoneReferenceHttpResponse.FromModel).ToArray(),
        model.Cultures.Select(CultureReferenceHttpResponse.FromModel).ToArray(),
        model.TaxIdTypes.Select(TaxIdTypeReferenceHttpResponse.FromModel).ToArray());
}

public sealed record CurrencyReferenceHttpResponse(
    string Code, string NumericCode, int? MinorUnits, string Symbol,
    string DisplaySymbol, string Name, string NamePlural, bool IsEnabled, int? SortOrder)
{
    public static CurrencyReferenceHttpResponse FromModel(CurrencyReferenceItem item) => new(
        item.Code, item.NumericCode, item.MinorUnits, item.Symbol, item.DisplaySymbol,
        item.Name, item.NamePlural, item.IsEnabled, item.SortOrder);
}

public sealed record CountryReferenceHttpResponse(
    string Code, string Alpha3, string NumericCode, string? CallingCode,
    string? DefaultCurrencyCode, string? DefaultTimeZoneId, string Name, bool IsEnabled, int? SortOrder)
{
    public static CountryReferenceHttpResponse FromModel(CountryReferenceItem item) => new(
        item.Code, item.Alpha3, item.NumericCode, item.CallingCode, item.DefaultCurrencyCode,
        item.DefaultTimeZoneId, item.Name, item.IsEnabled, item.SortOrder);
}

public sealed record TimeZoneReferenceHttpResponse(
    string Id, IReadOnlyList<string> CountryCodes, string City, bool IsEnabled, int? SortOrder)
{
    public static TimeZoneReferenceHttpResponse FromModel(TimeZoneReferenceItem item) => new(
        item.Id, item.CountryCodes, item.City, item.IsEnabled, item.SortOrder);
}

public sealed record CultureReferenceHttpResponse(
    string Code, string LanguageCode, string CountryCode,
    string DatePattern, string TimePattern, string DateTimePattern, string LongDatePattern,
    string AmDesignator, string PmDesignator,
    string DecimalSeparator, string GroupSeparator, string CurrencyPattern, string PercentPattern,
    string? FallbackCulture, bool IsDefault, string Name, bool IsEnabled, int? SortOrder)
{
    public static CultureReferenceHttpResponse FromModel(CultureReferenceItem item) => new(
        item.Code, item.LanguageCode, item.CountryCode, item.DatePattern, item.TimePattern,
        item.DateTimePattern, item.LongDatePattern, item.AmDesignator, item.PmDesignator,
        item.DecimalSeparator, item.GroupSeparator,
        item.CurrencyPattern, item.PercentPattern, item.FallbackCulture, item.IsDefault,
        item.Name, item.IsEnabled, item.SortOrder);
}

public sealed record TaxIdTypeReferenceHttpResponse(
    string Code, string CountryCode, string Label, string Mask, string ValidatorKey,
    string AppliesTo, string Name, bool IsEnabled, int? SortOrder)
{
    public static TaxIdTypeReferenceHttpResponse FromModel(TaxIdTypeReferenceItem item) => new(
        item.Code, item.CountryCode, item.Label, item.Mask, item.ValidatorKey,
        item.AppliesTo, item.Name, item.IsEnabled, item.SortOrder);
}
