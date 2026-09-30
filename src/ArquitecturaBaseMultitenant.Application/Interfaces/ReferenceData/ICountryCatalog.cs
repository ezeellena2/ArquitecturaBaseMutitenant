namespace ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

/// <summary>Consulta países y traducciones del catálogo global para validación y presentación; devuelve también filas deshabilitadas.</summary>
public interface ICountryCatalog
{
    Task<IReadOnlyList<CountryCatalogEntry>> ListAsync(CancellationToken cancellationToken);
    Task<CountryCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken);
}

public sealed record CountryCatalogEntry(
    string Code,
    string Alpha3,
    string NumericCode,
    string? CallingCode,
    string? DefaultCurrencyCode,
    string? DefaultTimeZoneId,
    bool IsEnabled,
    int? SortOrder,
    IReadOnlyList<CountryCatalogTranslation> Translations);

public sealed record CountryCatalogTranslation(string Culture, string Name);
