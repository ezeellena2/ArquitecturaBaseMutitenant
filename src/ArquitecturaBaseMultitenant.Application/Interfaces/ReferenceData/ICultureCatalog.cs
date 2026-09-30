namespace ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

/// <summary>Consulta culturas y perfiles de formato del catálogo global; los casos de uso deciden cuándo exigir que estén habilitados.</summary>
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
    string AmDesignator,
    string PmDesignator,
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
