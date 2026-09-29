namespace ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

public interface ICultureCatalog
{
    Task<IReadOnlyList<CultureCatalogEntry>> ListAsync(CancellationToken cancellationToken);
    Task<CultureCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken);
}

public sealed record CultureCatalogEntry(
    string Code,
    string LanguageCode,
    string CountryCode,
    string DatePattern,
    string TimePattern,
    string DateTimePattern,
    string LongDatePattern,
    string DecimalSeparator,
    string GroupSeparator,
    string CurrencyPattern,
    string PercentPattern,
    string? FallbackCulture,
    bool IsEnabled,
    bool IsDefault,
    int? SortOrder,
    IReadOnlyList<CultureCatalogTranslation> Translations);

public sealed record CultureCatalogTranslation(string DisplayCulture, string Name);
