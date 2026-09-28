namespace ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

public interface ITaxIdTypeCatalog
{
    Task<IReadOnlyList<TaxIdTypeCatalogEntry>> ListAsync(CancellationToken cancellationToken);
    Task<TaxIdTypeCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken);
}

public sealed record TaxIdTypeCatalogEntry(
    string Code,
    string CountryCode,
    string Label,
    string Mask,
    string ValidatorKey,
    string AppliesTo,
    bool IsEnabled,
    int? SortOrder,
    IReadOnlyList<TaxIdTypeCatalogTranslation> Translations);

public sealed record TaxIdTypeCatalogTranslation(string Culture, string Name);
