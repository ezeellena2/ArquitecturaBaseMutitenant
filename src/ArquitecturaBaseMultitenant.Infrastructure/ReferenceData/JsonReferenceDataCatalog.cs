using System.Collections.ObjectModel;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

namespace ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;

public sealed class JsonReferenceDataCatalog :
    ICurrencyCatalog,
    ICountryCatalog,
    ITimeZoneCatalog,
    ICultureCatalog,
    ITaxIdTypeCatalog
{
    private const string ResourcePrefix = "ReferenceData.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Snapshot<CurrencyCatalogEntry> _currencies =
        Read<CurrencyCatalogEntry>("currencies.json", "Currencies", entry => entry.Code);

    private readonly Snapshot<CountryCatalogEntry> _countries =
        Read<CountryCatalogEntry>("countries.json", "Countries", entry => entry.Code);

    private readonly Snapshot<TimeZoneCatalogEntry> _timeZones =
        Read<TimeZoneCatalogEntry>("time-zones.json", "TimeZones", entry => entry.Id);

    private readonly Snapshot<CultureCatalogEntry> _cultures =
        Read<CultureCatalogEntry>("cultures.json", "Cultures", entry => entry.Code);

    private readonly Snapshot<TaxIdTypeCatalogEntry> _taxIdTypes =
        Read<TaxIdTypeCatalogEntry>("tax-id-types.json", "TaxIdTypes", entry => entry.Code);

    Task<IReadOnlyList<CurrencyCatalogEntry>> ICurrencyCatalog.ListAsync(CancellationToken cancellationToken) =>
        ListAsync(_currencies, cancellationToken);

    Task<CurrencyCatalogEntry?> ICurrencyCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
        FindAsync(_currencies, code, cancellationToken);

    Task<IReadOnlyList<CountryCatalogEntry>> ICountryCatalog.ListAsync(CancellationToken cancellationToken) =>
        ListAsync(_countries, cancellationToken);

    Task<CountryCatalogEntry?> ICountryCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
        FindAsync(_countries, code, cancellationToken);

    Task<IReadOnlyList<TimeZoneCatalogEntry>> ITimeZoneCatalog.ListAsync(CancellationToken cancellationToken) =>
        ListAsync(_timeZones, cancellationToken);

    Task<TimeZoneCatalogEntry?> ITimeZoneCatalog.FindAsync(string id, CancellationToken cancellationToken) =>
        FindAsync(_timeZones, id, cancellationToken);

    Task<IReadOnlyList<CultureCatalogEntry>> ICultureCatalog.ListAsync(CancellationToken cancellationToken) =>
        ListAsync(_cultures, cancellationToken);

    Task<CultureCatalogEntry?> ICultureCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
        FindAsync(_cultures, code, cancellationToken);

    Task<IReadOnlyList<TaxIdTypeCatalogEntry>> ITaxIdTypeCatalog.ListAsync(CancellationToken cancellationToken) =>
        ListAsync(_taxIdTypes, cancellationToken);

    Task<TaxIdTypeCatalogEntry?> ITaxIdTypeCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
        FindAsync(_taxIdTypes, code, cancellationToken);

    private static Task<IReadOnlyList<T>> ListAsync<T>(Snapshot<T> snapshot, CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<T>>(snapshot.Items);
    }

    private static Task<T?> FindAsync<T>(Snapshot<T> snapshot, string code, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(code);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(snapshot.Find(code));
    }

    private static Snapshot<T> Read<T>(string resourceFileName, string arrayName, Func<T, string> keySelector)
        where T : class
    {
        var assembly = typeof(JsonReferenceDataCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + resourceFileName)
            ?? throw new InvalidOperationException($"Embedded reference data {resourceFileName} is missing.");
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty(arrayName, out var array))
        {
            throw new InvalidOperationException($"Embedded reference data {resourceFileName} has no {arrayName} array.");
        }

        var items = array.Deserialize<T[]>(JsonOptions)
            ?? throw new InvalidOperationException($"Embedded reference data {resourceFileName} could not be read.");
        if (items.Length == 0)
        {
            throw new InvalidOperationException($"Embedded reference data {resourceFileName} is empty.");
        }

        return new Snapshot<T>(items, keySelector);
    }

    private sealed class Snapshot<T>(T[] items, Func<T, string> keySelector)
        where T : class
    {
        private readonly Dictionary<string, T> _byKey = items.ToDictionary(keySelector, StringComparer.Ordinal);

        public ReadOnlyCollection<T> Items { get; } = Array.AsReadOnly(items);

        public T? Find(string key) => _byKey.GetValueOrDefault(key);
    }
}
