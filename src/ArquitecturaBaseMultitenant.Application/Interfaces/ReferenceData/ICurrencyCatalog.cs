namespace ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

public interface ICurrencyCatalog
{
    Task<IReadOnlyList<CurrencyCatalogEntry>> ListAsync(CancellationToken cancellationToken);
    Task<CurrencyCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken);
}

public sealed record CurrencyCatalogEntry(
    string Code,
    string NumericCode,
    int? MinorUnits,
    string Symbol,
    bool IsEnabled,
    int? SortOrder,
    IReadOnlyList<CurrencyCatalogTranslation> Translations);

public sealed record CurrencyCatalogTranslation(
    string Culture,
    string Name,
    string NamePlural,
    string DisplaySymbol);
