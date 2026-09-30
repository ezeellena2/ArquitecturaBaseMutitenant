namespace ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

/// <summary>Consulta zonas IANA y sus nombres traducidos desde el catálogo global; el cálculo horario queda en el servicio de tiempo.</summary>
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
