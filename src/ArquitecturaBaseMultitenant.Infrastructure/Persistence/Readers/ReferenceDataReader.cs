using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Lee los cinco catálogos globales de platform sin filtrar filas históricas deshabilitadas.</summary>
internal sealed class ReferenceDataReader(
    IServiceScopeFactory scopes,
    HybridCache cache) :
    ICurrencyCatalog, ICountryCatalog, ITimeZoneCatalog, ICultureCatalog, ITaxIdTypeCatalog
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromHours(1),
    };

    Task<IReadOnlyList<CurrencyCatalogEntry>> ICurrencyCatalog.ListAsync(CancellationToken cancellationToken) =>
        ReadAsync(ReferenceDataCache.CurrenciesKey, LoadCurrenciesAsync, cancellationToken);

    async Task<CurrencyCatalogEntry?> ICurrencyCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
        (await ((ICurrencyCatalog)this).ListAsync(cancellationToken)).FirstOrDefault(
            item => string.Equals(item.Code, code, StringComparison.Ordinal));

    Task<IReadOnlyList<CountryCatalogEntry>> ICountryCatalog.ListAsync(CancellationToken cancellationToken) =>
        ReadAsync(ReferenceDataCache.CountriesKey, LoadCountriesAsync, cancellationToken);

    async Task<CountryCatalogEntry?> ICountryCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
        (await ((ICountryCatalog)this).ListAsync(cancellationToken)).FirstOrDefault(
            item => string.Equals(item.Code, code, StringComparison.Ordinal));

    Task<IReadOnlyList<TimeZoneCatalogEntry>> ITimeZoneCatalog.ListAsync(CancellationToken cancellationToken) =>
        ReadAsync(ReferenceDataCache.TimeZonesKey, LoadTimeZonesAsync, cancellationToken);

    async Task<TimeZoneCatalogEntry?> ITimeZoneCatalog.FindAsync(string id, CancellationToken cancellationToken) =>
        (await ((ITimeZoneCatalog)this).ListAsync(cancellationToken)).FirstOrDefault(
            item => string.Equals(item.Id, id, StringComparison.Ordinal));

    Task<IReadOnlyList<CultureCatalogEntry>> ICultureCatalog.ListAsync(CancellationToken cancellationToken) =>
        ReadAsync(ReferenceDataCache.CulturesKey, LoadCulturesAsync, cancellationToken);

    async Task<CultureCatalogEntry?> ICultureCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
        (await ((ICultureCatalog)this).ListAsync(cancellationToken)).FirstOrDefault(
            item => string.Equals(item.Code, code, StringComparison.Ordinal));

    Task<IReadOnlyList<TaxIdTypeCatalogEntry>> ITaxIdTypeCatalog.ListAsync(CancellationToken cancellationToken) =>
        ReadAsync(ReferenceDataCache.TaxIdTypesKey, LoadTaxIdTypesAsync, cancellationToken);

    async Task<TaxIdTypeCatalogEntry?> ITaxIdTypeCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
        (await ((ITaxIdTypeCatalog)this).ListAsync(cancellationToken)).FirstOrDefault(
            item => string.Equals(item.Code, code, StringComparison.Ordinal));

    private async Task<IReadOnlyList<TEntry>> ReadAsync<TEntry>(string key,
        Func<ApplicationDbContext, CancellationToken, Task<TEntry[]>> load,
        CancellationToken cancellationToken) =>
        await cache.GetOrCreateInOwnScopeAsync<ApplicationDbContext, TEntry[]>(
            key, scopes, load, CacheOptions, cancellationToken);

    private static async Task<CurrencyCatalogEntry[]> LoadCurrenciesAsync(
        ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var rows = await context.Set<Currency>().AsNoTracking().ToArrayAsync(cancellationToken);
        var translations = (await context.Set<CurrencyTranslation>().AsNoTracking()
            .ToArrayAsync(cancellationToken)).ToLookup(item => Code(item.CurrencyCode), StringComparer.Ordinal);

        return rows.Select(row => new CurrencyCatalogEntry(
                Code(row.Code), Code(row.NumericCode), row.MinorUnits, row.Symbol,
                row.IsEnabled, row.SortOrder,
                translations[Code(row.Code)].Select(item => new CurrencyCatalogTranslation(
                    item.Culture, item.Name, item.NamePlural, item.DisplaySymbol)).ToArray()))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<CountryCatalogEntry[]> LoadCountriesAsync(
        ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var rows = await context.Set<Country>().AsNoTracking().ToArrayAsync(cancellationToken);
        var translations = (await context.Set<CountryTranslation>().AsNoTracking()
            .ToArrayAsync(cancellationToken)).ToLookup(item => Code(item.CountryCode), StringComparer.Ordinal);

        return rows.Select(row => new CountryCatalogEntry(
                Code(row.Code), Code(row.Alpha3), Code(row.NumericCode), row.CallingCode,
                row.DefaultCurrencyCode is null ? null : Code(row.DefaultCurrencyCode),
                row.DefaultTimeZoneId, row.IsEnabled, row.SortOrder,
                translations[Code(row.Code)].Select(item => new CountryCatalogTranslation(
                    item.Culture, item.Name)).ToArray()))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<TimeZoneCatalogEntry[]> LoadTimeZonesAsync(
        ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var rows = await context.Set<ReferenceTimeZone>().AsNoTracking().ToArrayAsync(cancellationToken);
        var countries = (await context.Set<TimeZoneCountry>().AsNoTracking()
            .Where(item => item.IsEnabled).ToArrayAsync(cancellationToken))
            .ToLookup(item => item.TimeZoneId, StringComparer.Ordinal);
        var translations = (await context.Set<TimeZoneTranslation>().AsNoTracking()
            .ToArrayAsync(cancellationToken)).ToLookup(item => item.TimeZoneId, StringComparer.Ordinal);

        return rows.Select(row => new TimeZoneCatalogEntry(
                row.Id,
                countries[row.Id].Select(item => Code(item.CountryCode))
                    .OrderBy(code => code, StringComparer.Ordinal).ToArray(),
                row.IsEnabled, row.SortOrder,
                translations[row.Id].Select(item => new TimeZoneCatalogTranslation(
                    item.Culture, item.City)).ToArray()))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<CultureCatalogEntry[]> LoadCulturesAsync(
        ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var rows = await context.Set<Culture>().AsNoTracking().ToArrayAsync(cancellationToken);
        var translations = (await context.Set<CultureTranslation>().AsNoTracking()
            .ToArrayAsync(cancellationToken)).ToLookup(item => item.CultureCode, StringComparer.Ordinal);

        return rows.Select(row => new CultureCatalogEntry(
                row.Code, row.LanguageCode, Code(row.CountryCode), row.DatePattern,
                row.TimePattern, row.DateTimePattern, row.LongDatePattern,
                row.AmDesignator, row.PmDesignator, row.DecimalSeparator,
                row.GroupSeparator, row.CurrencyPattern, row.PercentPattern, row.FallbackCulture,
                row.IsEnabled, row.IsDefault, row.SortOrder,
                translations[row.Code].Select(item => new CultureCatalogTranslation(
                    item.DisplayCulture, item.Name)).ToArray()))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<TaxIdTypeCatalogEntry[]> LoadTaxIdTypesAsync(
        ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var rows = await context.Set<TaxIdType>().AsNoTracking().ToArrayAsync(cancellationToken);
        var translations = (await context.Set<TaxIdTypeTranslation>().AsNoTracking()
            .ToArrayAsync(cancellationToken)).ToLookup(item => item.TaxIdTypeCode, StringComparer.Ordinal);

        return rows.Select(row => new TaxIdTypeCatalogEntry(
                row.Code, Code(row.CountryCode), row.Label, row.Mask, row.ValidatorKey,
                row.AppliesTo, row.IsEnabled, row.SortOrder,
                translations[row.Code].Select(item => new TaxIdTypeCatalogTranslation(
                    item.Culture, item.Name)).ToArray()))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Code(string value) => value.TrimEnd();
}
