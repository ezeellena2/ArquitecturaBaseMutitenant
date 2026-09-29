using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.ReferenceData;

[CollectionDefinition("ReferenceDataCatalog", DisableParallelization = true)]
public sealed class ReferenceDataCatalogTestsGroup;

[Collection("ReferenceDataCatalog")]
public sealed class ReferenceDataCatalogTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Embedded_catalogs_can_be_read_without_the_checkout_as_current_directory()
    {
        var previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            var catalog = new JsonReferenceDataCatalog();

            Assert.NotNull(await ((ICurrencyCatalog)catalog).FindAsync("ARS", CancellationToken));
            Assert.NotNull(await ((ICountryCatalog)catalog).FindAsync("AR", CancellationToken));
            Assert.NotNull(await ((ITimeZoneCatalog)catalog).FindAsync("UTC", CancellationToken));
            Assert.NotNull(await ((ICultureCatalog)catalog).FindAsync("es-AR", CancellationToken));
            Assert.NotNull(await ((ITaxIdTypeCatalog)catalog).FindAsync("AR-CUIT", CancellationToken));
        }
        finally
        {
            Environment.CurrentDirectory = previous;
        }
    }

    [Fact]
    public async Task Disabled_rows_remain_readable_but_have_disabled_flag()
    {
        var catalog = new JsonReferenceDataCatalog();
        var currencies = (ICurrencyCatalog)catalog;
        var countries = (ICountryCatalog)catalog;
        var zones = (ITimeZoneCatalog)catalog;

        Assert.True((await currencies.FindAsync("ARS", CancellationToken))!.IsEnabled);
        Assert.False((await currencies.FindAsync("AED", CancellationToken))!.IsEnabled);
        Assert.True((await countries.FindAsync("AR", CancellationToken))!.IsEnabled);
        Assert.False((await countries.FindAsync("AD", CancellationToken))!.IsEnabled);
        Assert.True((await zones.FindAsync("UTC", CancellationToken))!.IsEnabled);
        Assert.False((await zones.FindAsync("Europe/Andorra", CancellationToken))!.IsEnabled);
        Assert.Null(await currencies.FindAsync("ZZZ", CancellationToken));
        Assert.Null(await countries.FindAsync("ZZ", CancellationToken));
        Assert.Null(await zones.FindAsync("Unknown/Zone", CancellationToken));
    }

    [Fact]
    public async Task Currency_minor_units_and_symbols_come_from_the_snapshot()
    {
        var currencies = await ((ICurrencyCatalog)new JsonReferenceDataCatalog()).ListAsync(CancellationToken);

        Assert.Contains(currencies, currency => currency.MinorUnits == 0);
        Assert.Contains(currencies, currency => currency.MinorUnits == 2);
        Assert.Contains(currencies, currency => currency.MinorUnits == 3);
        Assert.All(currencies.Where(currency => currency.IsEnabled), currency => Assert.NotNull(currency.MinorUnits));
        Assert.Contains(currencies, currency => currency.Translations.Any(translation =>
            translation.Culture == "en-US" && translation.DisplaySymbol != currency.Symbol));
    }

    [Fact]
    public async Task Countries_and_cultures_have_valid_references_without_inventing_missing_data()
    {
        var catalog = new JsonReferenceDataCatalog();
        var currencies = (await ((ICurrencyCatalog)catalog).ListAsync(CancellationToken)).ToDictionary(row => row.Code);
        var countries = (await ((ICountryCatalog)catalog).ListAsync(CancellationToken)).ToDictionary(row => row.Code);
        var zones = (await ((ITimeZoneCatalog)catalog).ListAsync(CancellationToken)).ToDictionary(row => row.Id);
        var cultures = await ((ICultureCatalog)catalog).ListAsync(CancellationToken);
        var taxTypes = await ((ITaxIdTypeCatalog)catalog).ListAsync(CancellationToken);

        Assert.Contains(countries.Values, country => country.DefaultTimeZoneId is null && !country.IsEnabled);
        foreach (var country in countries.Values)
        {
            if (country.DefaultCurrencyCode is not null)
            {
                Assert.Contains(country.DefaultCurrencyCode, currencies.Keys);
            }

            if (country.DefaultTimeZoneId is not null)
            {
                Assert.Contains(country.DefaultTimeZoneId, zones.Keys);
                Assert.Contains(country.Code, zones[country.DefaultTimeZoneId].CountryCodes);
            }

            if (country.IsEnabled)
            {
                Assert.NotNull(country.CallingCode);
                Assert.NotNull(country.DefaultCurrencyCode);
                Assert.NotNull(country.DefaultTimeZoneId);
                Assert.True(currencies[country.DefaultCurrencyCode].IsEnabled);
            }
        }

        Assert.All(zones.Values.Where(zone => zone.IsEnabled && zone.Id != "UTC"), zone =>
            Assert.Contains(zone.CountryCodes, code => countries[code].IsEnabled));
        Assert.All(taxTypes.Where(taxType => taxType.IsEnabled), taxType =>
            Assert.True(countries[taxType.CountryCode].IsEnabled));

        foreach (var culture in cultures)
        {
            Assert.Contains(culture.CountryCode, countries.Keys);
        }
    }

    [Fact]
    public async Task Shared_zones_keep_every_country_and_utc_has_none()
    {
        var catalog = new JsonReferenceDataCatalog();
        var countries = (await ((ICountryCatalog)catalog).ListAsync(CancellationToken))
            .Select(country => country.Code).ToHashSet();
        var zones = await ((ITimeZoneCatalog)catalog).ListAsync(CancellationToken);

        Assert.Empty(Assert.Single(zones, zone => zone.Id == "UTC").CountryCodes);
        var shared = Assert.Single(zones, zone => zone.Id == "Asia/Dubai");
        Assert.Contains("AE", shared.CountryCodes);
        Assert.Contains("OM", shared.CountryCodes);
        Assert.True(shared.CountryCodes.Count > 1);
        Assert.All(zones.SelectMany(zone => zone.CountryCodes), country => Assert.Contains(country, countries));
    }

    [Fact]
    public async Task Cultures_and_tax_types_keep_translations_and_country_links()
    {
        var catalog = new JsonReferenceDataCatalog();
        var cultures = await ((ICultureCatalog)catalog).ListAsync(CancellationToken);
        var countries = (await ((ICountryCatalog)catalog).ListAsync(CancellationToken))
            .Select(country => country.Code).ToHashSet();
        var taxTypes = await ((ITaxIdTypeCatalog)catalog).ListAsync(CancellationToken);
        var defaultCulture = Assert.Single(cultures, culture => culture.IsDefault);

        Assert.Equal("Español (Argentina)", defaultCulture.DisplayName("fr-FR", defaultCulture.Code));
        Assert.Equal("Spanish (Argentina)", defaultCulture.DisplayName("en-US", defaultCulture.Code));
        Assert.All(cultures, culture => Assert.All(cultures.Where(display => display.IsEnabled), display =>
            Assert.Contains(culture.Translations, translation => translation.DisplayCulture == display.Code)));
        Assert.All(taxTypes, taxType => Assert.Contains(taxType.CountryCode, countries));
        Assert.Contains(taxTypes, taxType => taxType.IsEnabled);
    }

    [Fact]
    public async Task Every_catalog_row_has_a_translation_for_each_enabled_culture()
    {
        var catalog = new JsonReferenceDataCatalog();
        var currencies = await ((ICurrencyCatalog)catalog).ListAsync(CancellationToken);
        var countries = await ((ICountryCatalog)catalog).ListAsync(CancellationToken);
        var zones = await ((ITimeZoneCatalog)catalog).ListAsync(CancellationToken);
        var cultures = await ((ICultureCatalog)catalog).ListAsync(CancellationToken);
        var taxTypes = await ((ITaxIdTypeCatalog)catalog).ListAsync(CancellationToken);

        foreach (var culture in cultures.Where(culture => culture.IsEnabled))
        {
            Assert.All(currencies, currency => Assert.Contains(currency.Translations, translation =>
                translation.Culture == culture.Code && !string.IsNullOrWhiteSpace(translation.Name)
                && !string.IsNullOrWhiteSpace(translation.DisplaySymbol)));
            Assert.All(countries, country => Assert.Contains(country.Translations, translation =>
                translation.Culture == culture.Code && !string.IsNullOrWhiteSpace(translation.Name)));
            Assert.All(zones, zone => Assert.Contains(zone.Translations, translation =>
                translation.Culture == culture.Code && !string.IsNullOrWhiteSpace(translation.City)));
            Assert.All(cultures, translatedCulture => Assert.Contains(translatedCulture.Translations, translation =>
                translation.DisplayCulture == culture.Code && !string.IsNullOrWhiteSpace(translation.Name)));
            Assert.All(taxTypes, taxType => Assert.Contains(taxType.Translations, translation =>
                translation.Culture == culture.Code && !string.IsNullOrWhiteSpace(translation.Name)));
        }
    }

}
