using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

internal sealed class ReferenceDataSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JsonReferenceDataCatalog _source;
    private readonly ReferenceDataCache _referenceCache;

    public ReferenceDataSeeder(ApplicationDbContext dbContext, IUnitOfWork unitOfWork,
        JsonReferenceDataCatalog source, ReferenceDataCache referenceCache)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _referenceCache = referenceCache ?? throw new ArgumentNullException(nameof(referenceCache));
    }

    public async Task<bool> SeedAsync(CancellationToken cancellationToken)
    {
        var snapshot = await ReferenceDataSeedSnapshot.LoadAsync(_source, cancellationToken);
        var result = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _dbContext.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.ReferenceDataSeed], ct);
            return Result.Success(await StageAsync(snapshot, ct));
        }, CommitPolicy.OnSuccess, cancellationToken);
        if (result.Value)
        {
            await _referenceCache.InvalidateAsync(cancellationToken);
        }

        return result.Value;
    }

    internal async Task<bool> StageAsync(ReferenceDataSeedSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _dbContext.RequireTransaction();

        await UpsertAsync(_dbContext.Set<Currency>(), snapshot.Currencies.Select(item =>
            Currency.Create(item.Code, item.NumericCode, item.MinorUnits, item.Symbol,
                item.IsEnabled, item.SortOrder)), item => Key(item.Code), disableMissing: true, cancellationToken);

        await UpsertAsync(_dbContext.Set<ReferenceTimeZone>(), snapshot.TimeZones.Select(item =>
            ReferenceTimeZone.Create(item.Id, item.IsEnabled, item.SortOrder)),
            item => Key(item.Id), disableMissing: true, cancellationToken);

        await UpsertAsync(_dbContext.Set<Country>(), snapshot.Countries.Select(item =>
            Country.Create(item.Code, item.Alpha3, item.NumericCode, item.CallingCode,
                item.DefaultCurrencyCode, item.DefaultTimeZoneId, item.IsEnabled, item.SortOrder)),
            item => Key(item.Code), disableMissing: true, cancellationToken);

        await UpsertAsync(_dbContext.Set<Culture>(), snapshot.Cultures.Select(item =>
            Culture.Create(item.Code, item.LanguageCode, item.CountryCode, item.DatePattern,
                item.TimePattern, item.DateTimePattern, item.LongDatePattern,
                item.AmDesignator, item.PmDesignator,
                item.DecimalSeparator, item.GroupSeparator, item.CurrencyPattern,
                item.PercentPattern, item.FallbackCulture, item.IsEnabled, item.IsDefault,
                item.SortOrder)), item => Key(item.Code), disableMissing: true, cancellationToken);

        await UpsertAsync(_dbContext.Set<TaxIdType>(), snapshot.TaxIdTypes.Select(item =>
            TaxIdType.Create(item.Code, item.CountryCode, item.Label, item.Mask,
                item.ValidatorKey, item.AppliesTo, item.IsEnabled, item.SortOrder)),
            item => Key(item.Code), disableMissing: true, cancellationToken);

        await UpsertAsync(_dbContext.Set<CurrencyTranslation>(), snapshot.Currencies.SelectMany(currency =>
            currency.Translations.Select(item => CurrencyTranslation.Create(currency.Code, item.Culture,
                item.Name, item.NamePlural, item.DisplaySymbol))),
            item => (Key(item.CurrencyCode), Key(item.Culture)), disableMissing: false, cancellationToken);

        await UpsertAsync(_dbContext.Set<CountryTranslation>(), snapshot.Countries.SelectMany(country =>
            country.Translations.Select(item => CountryTranslation.Create(country.Code, item.Culture, item.Name))),
            item => (Key(item.CountryCode), Key(item.Culture)), disableMissing: false, cancellationToken);

        await UpsertAsync(_dbContext.Set<TimeZoneTranslation>(), snapshot.TimeZones.SelectMany(zone =>
            zone.Translations.Select(item => TimeZoneTranslation.Create(zone.Id, item.Culture, item.City))),
            item => (Key(item.TimeZoneId), Key(item.Culture)), disableMissing: false, cancellationToken);

        await UpsertAsync(_dbContext.Set<CultureTranslation>(), snapshot.Cultures.SelectMany(culture =>
            culture.Translations.Select(item => CultureTranslation.Create(culture.Code,
                item.DisplayCulture, item.Name))),
            item => (Key(item.CultureCode), Key(item.DisplayCulture)), disableMissing: false, cancellationToken);

        await UpsertAsync(_dbContext.Set<TaxIdTypeTranslation>(), snapshot.TaxIdTypes.SelectMany(type =>
            type.Translations.Select(item => TaxIdTypeTranslation.Create(type.Code, item.Culture, item.Name))),
            item => (Key(item.TaxIdTypeCode), Key(item.Culture)), disableMissing: false, cancellationToken);

        await UpsertAsync(_dbContext.Set<TimeZoneCountry>(), snapshot.TimeZones.SelectMany(zone =>
            zone.CountryCodes.Select(countryCode => TimeZoneCountry.Create(zone.Id, countryCode,
                isEnabled: true))),
            item => (Key(item.TimeZoneId), Key(item.CountryCode)), disableMissing: true, cancellationToken);

        return _dbContext.ChangeTracker.HasChanges();
    }

    private async Task UpsertAsync<TEntity, TKey>(DbSet<TEntity> set, IEnumerable<TEntity> sourceRows,
        Func<TEntity, TKey> keySelector, bool disableMissing, CancellationToken cancellationToken)
        where TEntity : class
        where TKey : notnull
    {
        var existing = (await set.ToListAsync(cancellationToken)).ToDictionary(keySelector);
        var seen = new HashSet<TKey>();

        foreach (var desired in sourceRows)
        {
            var key = keySelector(desired);
            if (!seen.Add(key))
            {
                throw new InvalidOperationException($"Reference data contains a duplicate {typeof(TEntity).Name} key.");
            }

            if (!existing.TryGetValue(key, out var current))
            {
                set.Add(desired);
                continue;
            }

            var currentEntry = _dbContext.Entry(current);
            var desiredEntry = _dbContext.Entry(desired);
            foreach (var property in currentEntry.Metadata.GetProperties())
            {
                if (!property.IsPrimaryKey())
                {
                    currentEntry.Property(property.Name).CurrentValue = desiredEntry.Property(property.Name).CurrentValue;
                }
            }
        }

        if (disableMissing)
        {
            foreach (var current in existing.Values.Where(item => !seen.Contains(keySelector(item))))
            {
                _dbContext.Entry(current).Property(nameof(Currency.IsEnabled)).CurrentValue = false;
            }
        }
    }

    private static string Key(string value) => value.TrimEnd();
}

internal sealed record ReferenceDataSeedSnapshot(
    IReadOnlyList<CurrencyCatalogEntry> Currencies,
    IReadOnlyList<CountryCatalogEntry> Countries,
    IReadOnlyList<TimeZoneCatalogEntry> TimeZones,
    IReadOnlyList<CultureCatalogEntry> Cultures,
    IReadOnlyList<TaxIdTypeCatalogEntry> TaxIdTypes)
{
    public static async Task<ReferenceDataSeedSnapshot> LoadAsync(
        JsonReferenceDataCatalog source, CancellationToken cancellationToken) =>
        new(
            await ((ICurrencyCatalog)source).ListAsync(cancellationToken),
            await ((ICountryCatalog)source).ListAsync(cancellationToken),
            await ((ITimeZoneCatalog)source).ListAsync(cancellationToken),
            await ((ICultureCatalog)source).ListAsync(cancellationToken),
            await ((ITaxIdTypeCatalog)source).ListAsync(cancellationToken));
}
