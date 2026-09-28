using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.ReferenceData;

internal sealed class ReferenceDataService(
    ICurrencyCatalog currencies,
    ICountryCatalog countries,
    ITimeZoneCatalog timeZones,
    ICultureCatalog cultures,
    ITaxIdTypeCatalog taxIdTypes,
    TimeProvider timeProvider,
    ILogger<ReferenceDataService> logger) : IReferenceDataService
{
    public Task<Result<ReferenceDataResponse>> GetAllAsync(string? culture, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "GetReferenceData", async () =>
        {
            var selectedCulture = await SelectCultureAsync(culture, cancellationToken);
            var response = new ReferenceDataResponse(
                selectedCulture.Code,
                await LoadCurrenciesAsync(selectedCulture, null, cancellationToken),
                await LoadCountriesAsync(selectedCulture, null, cancellationToken),
                await LoadTimeZonesAsync(selectedCulture, null, cancellationToken),
                await LoadCulturesAsync(selectedCulture, null, cancellationToken),
                await LoadTaxIdTypesAsync(selectedCulture, null, cancellationToken));
            return Result.Success(response);
        });

    public Task<Result<IReadOnlyList<CurrencyReferenceItem>>> GetCurrenciesAsync(
        string? culture, string? search, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "GetCurrencies", async () =>
        {
            var selectedCulture = await SelectCultureAsync(culture, cancellationToken);
            return Result.Success(await LoadCurrenciesAsync(selectedCulture, search, cancellationToken));
        });

    public Task<Result<IReadOnlyList<CountryReferenceItem>>> GetCountriesAsync(
        string? culture, string? search, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "GetCountries", async () =>
        {
            var selectedCulture = await SelectCultureAsync(culture, cancellationToken);
            return Result.Success(await LoadCountriesAsync(selectedCulture, search, cancellationToken));
        });

    public Task<Result<IReadOnlyList<TimeZoneReferenceItem>>> GetTimeZonesAsync(
        string? culture, string? search, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "GetTimeZones", async () =>
        {
            var selectedCulture = await SelectCultureAsync(culture, cancellationToken);
            return Result.Success(await LoadTimeZonesAsync(selectedCulture, search, cancellationToken));
        });

    public Task<Result<IReadOnlyList<CultureReferenceItem>>> GetCulturesAsync(
        string? culture, string? search, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "GetCultures", async () =>
        {
            var selectedCulture = await SelectCultureAsync(culture, cancellationToken);
            return Result.Success(await LoadCulturesAsync(selectedCulture, search, cancellationToken));
        });

    public Task<Result<IReadOnlyList<TaxIdTypeReferenceItem>>> GetTaxIdTypesAsync(
        string? culture, string? search, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "GetTaxIdTypes", async () =>
        {
            var selectedCulture = await SelectCultureAsync(culture, cancellationToken);
            return Result.Success(await LoadTaxIdTypesAsync(selectedCulture, search, cancellationToken));
        });

    private async Task<IReadOnlyList<CurrencyReferenceItem>> LoadCurrenciesAsync(
        SelectedCulture selectedCulture, string? search, CancellationToken cancellationToken)
    {
        var entries = await currencies.ListAsync(cancellationToken);
        return entries.Where(entry => entry.IsEnabled)
            .Select(entry =>
            {
                var translation = Translate(entry.Translations, item => item.Culture, selectedCulture);
                return new CurrencyReferenceItem(
                    entry.Code, entry.NumericCode, entry.MinorUnits, entry.Symbol,
                    translation.DisplaySymbol, translation.Name, translation.NamePlural,
                    entry.IsEnabled, entry.SortOrder);
            })
            .Where(item => Matches(search, item.Code, item.Name))
            .OrderBy(item => item.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<CountryReferenceItem>> LoadCountriesAsync(
        SelectedCulture selectedCulture, string? search, CancellationToken cancellationToken)
    {
        var entries = await countries.ListAsync(cancellationToken);
        return entries.Where(entry => entry.IsEnabled)
            .Select(entry => new CountryReferenceItem(
                entry.Code, entry.Alpha3, entry.NumericCode, entry.CallingCode,
                entry.DefaultCurrencyCode, entry.DefaultTimeZoneId,
                Translate(entry.Translations, item => item.Culture, selectedCulture).Name,
                entry.IsEnabled, entry.SortOrder))
            .Where(item => Matches(search, item.Code, item.Name))
            .OrderBy(item => item.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<TimeZoneReferenceItem>> LoadTimeZonesAsync(
        SelectedCulture selectedCulture, string? search, CancellationToken cancellationToken)
    {
        var entries = await timeZones.ListAsync(cancellationToken);
        return entries.Where(entry => entry.IsEnabled)
            .Select(entry => new TimeZoneReferenceItem(
                entry.Id, entry.CountryCodes,
                Translate(entry.Translations, item => item.Culture, selectedCulture).City,
                entry.IsEnabled, entry.SortOrder))
            .Where(item => Matches(search, item.Id, item.City))
            .OrderBy(item => item.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.City, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<CultureReferenceItem>> LoadCulturesAsync(
        SelectedCulture selectedCulture, string? search, CancellationToken cancellationToken)
    {
        var entries = await cultures.ListAsync(cancellationToken);
        return entries.Where(entry => entry.IsEnabled)
            .Select(entry => new CultureReferenceItem(
                entry.Code, entry.LanguageCode, entry.CountryCode,
                entry.DatePattern, entry.TimePattern, entry.DateTimePattern,
                entry.LongDatePattern, entry.DecimalSeparator, entry.GroupSeparator,
                entry.CurrencyPattern, entry.PercentPattern, entry.FallbackCulture,
                entry.IsDefault,
                Translate(entry.Translations, item => item.DisplayCulture, selectedCulture).Name,
                entry.IsEnabled, entry.SortOrder))
            .Where(item => Matches(search, item.Code, item.Name))
            .OrderBy(item => item.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<TaxIdTypeReferenceItem>> LoadTaxIdTypesAsync(
        SelectedCulture selectedCulture, string? search, CancellationToken cancellationToken)
    {
        var entries = await taxIdTypes.ListAsync(cancellationToken);
        return entries.Where(entry => entry.IsEnabled)
            .Select(entry => new TaxIdTypeReferenceItem(
                entry.Code, entry.CountryCode, entry.Label, entry.Mask,
                entry.ValidatorKey, entry.AppliesTo,
                Translate(entry.Translations, item => item.Culture, selectedCulture).Name,
                entry.IsEnabled, entry.SortOrder))
            .Where(item => Matches(search, item.Code, item.Name))
            .OrderBy(item => item.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<SelectedCulture> SelectCultureAsync(string? requestedCode, CancellationToken cancellationToken)
    {
        var entries = await cultures.ListAsync(cancellationToken);
        var byCode = entries.ToDictionary(entry => entry.Code, StringComparer.OrdinalIgnoreCase);
        var defaultCulture = entries.SingleOrDefault(entry => entry.IsEnabled && entry.IsDefault)
            ?? throw new InvalidOperationException("The reference data has no enabled default culture.");

        if (requestedCode is null || !byCode.TryGetValue(requestedCode, out var requested) || !requested.IsEnabled)
        {
            requested = defaultCulture;
        }

        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = requested;
        while (seen.Add(current.Code))
        {
            order.Add(current.Code);
            if (current.FallbackCulture is null ||
                !byCode.TryGetValue(current.FallbackCulture, out var fallback))
            {
                break;
            }

            current = fallback;
        }

        if (seen.Add(defaultCulture.Code))
        {
            order.Add(defaultCulture.Code);
        }

        return new SelectedCulture(requested.Code, order);
    }

    private static TTranslation Translate<TTranslation>(
        IReadOnlyList<TTranslation> translations,
        Func<TTranslation, string> cultureCode,
        SelectedCulture selectedCulture)
    {
        foreach (var code in selectedCulture.TranslationOrder)
        {
            foreach (var translation in translations)
            {
                if (string.Equals(cultureCode(translation), code, StringComparison.OrdinalIgnoreCase))
                {
                    return translation;
                }
            }
        }

        throw new InvalidOperationException("An enabled reference entry has no translation for its culture chain.");
    }

    private static bool Matches(string? search, params string[] values)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();
        return values.Any(value => CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            value, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
    }

    private sealed record SelectedCulture(string Code, IReadOnlyList<string> TranslationOrder);
}
