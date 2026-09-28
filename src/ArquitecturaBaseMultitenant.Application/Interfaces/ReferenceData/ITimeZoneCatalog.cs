namespace ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

public interface ITimeZoneCatalog
{
    Task<IReadOnlyList<TimeZoneCatalogEntry>> ListAsync(CancellationToken cancellationToken);
    Task<TimeZoneCatalogEntry?> FindAsync(string id, CancellationToken cancellationToken);
}

public sealed record TimeZoneCatalogEntry(
    string Id,
    IReadOnlyList<string> CountryCodes,
    bool IsEnabled,
    int? SortOrder,
    IReadOnlyList<TimeZoneCatalogTranslation> Translations);

public sealed record TimeZoneCatalogTranslation(string Culture, string City);
