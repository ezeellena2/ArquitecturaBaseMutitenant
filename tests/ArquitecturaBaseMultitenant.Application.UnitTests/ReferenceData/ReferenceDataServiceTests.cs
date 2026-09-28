using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.ReferenceData;

public sealed class ReferenceDataServiceTests
{
    [Fact]
    public async Task Aggregate_returns_all_five_catalogs_with_enabled_state_and_translations()
    {
        var (service, logger) = CreateService();

        var result = await service.GetAllAsync("en-US", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("en-US", response.Culture);
        Assert.Contains(response.Currencies, item => item.Code == "AED" && !item.IsEnabled
            && item.Name == "United Arab Emirates Dirham");
        Assert.Contains(response.Countries, item => item.Code == "AD" && !item.IsEnabled
            && item.Name == "Andorra");
        Assert.Contains(response.TimeZones, item => item.Id == "Europe/Andorra" && !item.IsEnabled
            && item.City == "Andorra");
        Assert.All(response.Cultures, item => Assert.True(item.IsEnabled));
        Assert.All(response.TaxIdTypes, item => Assert.True(item.IsEnabled));
        Assert.Equal("Argentine Peso", Assert.Single(response.Currencies, item => item.Code == "ARS").Name);
        Assert.Equal("ARS", Assert.Single(response.Currencies, item => item.Code == "ARS").DisplaySymbol);
        Assert.Equal(2, Assert.Single(response.Currencies, item => item.Code == "ARS").MinorUnits);
        Assert.Equal("Spanish (Argentina)", Assert.Single(response.Cultures, item => item.Code == "es-AR").Name);
        Assert.Contains(response.Countries, item => item.Code == "AR" && item.CallingCode == "54");
        Assert.Contains(response.TimeZones, item => item.Id == "America/Argentina/Buenos_Aires" && item.City == "Buenos Aires");
        Assert.Contains(response.TaxIdTypes, item => item.Code == "AR-CUIT" && item.ValidatorKey == "ar-cuit-mod11");
        Assert.Equal(["Handling GetReferenceData", "Handled GetReferenceData in 0 ms"],
            logger.Collector.GetSnapshot().Select(record => record.Message));
    }

    [Fact]
    public async Task Unknown_or_disabled_request_culture_falls_back_to_catalog_default()
    {
        var (service, _) = CreateService();

        var result = await service.GetAllAsync("fr-FR", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("es-AR", result.Value.Culture);
        Assert.Equal("peso argentino", Assert.Single(result.Value.Currencies, item => item.Code == "ARS").Name);
    }

    [Fact]
    public async Task Per_catalog_search_checks_code_and_translated_name_without_leaking_other_catalogs()
    {
        var (service, _) = CreateService();
        var ct = TestContext.Current.CancellationToken;

        var byCode = await service.GetCurrenciesAsync("es-AR", "ars", ct);
        var byName = await service.GetCountriesAsync("es-AR", "argentina", ct);
        var zones = await service.GetTimeZonesAsync("es-AR", "buenos", ct);
        var cultures = await service.GetCulturesAsync("en-US", "spanish", ct);
        var tax = await service.GetTaxIdTypesAsync("es-AR", "cuit", ct);
        var missing = await service.GetCurrenciesAsync("es-AR", "sin coincidencias", ct);

        Assert.Equal("ARS", Assert.Single(byCode.Value).Code);
        Assert.Equal("AR", Assert.Single(byName.Value).Code);
        Assert.Equal("America/Argentina/Buenos_Aires", Assert.Single(zones.Value).Id);
        Assert.Equal("es-AR", Assert.Single(cultures.Value).Code);
        Assert.Equal("AR-CUIT", Assert.Single(tax.Value).Code);
        Assert.Empty(missing.Value);
    }

    [Fact]
    public async Task Per_catalog_search_also_finds_disabled_historical_rows()
    {
        var (service, _) = CreateService();
        var ct = TestContext.Current.CancellationToken;

        var currency = await service.GetCurrenciesAsync("en-US", "AED", ct);
        var country = await service.GetCountriesAsync("en-US", "AD", ct);
        var zone = await service.GetTimeZonesAsync("en-US", "Europe/Andorra", ct);

        Assert.False(Assert.Single(currency.Value, item => item.Code == "AED").IsEnabled);
        Assert.False(Assert.Single(country.Value, item => item.Code == "AD").IsEnabled);
        Assert.False(Assert.Single(zone.Value, item => item.Id == "Europe/Andorra").IsEnabled);
    }

    [Fact]
    public async Task Tax_id_type_route_keeps_a_disabled_type_readable()
    {
        var catalog = new JsonReferenceDataCatalog();
        var service = new ReferenceDataService(
            catalog, catalog, catalog, catalog, new DisabledTaxIdTypeCatalog(catalog),
            new FakeTimeProvider(), new FakeLogger<ReferenceDataService>());

        var result = await service.GetTaxIdTypesAsync("es-AR", "AR-DNI", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(Assert.Single(result.Value).IsEnabled);
    }

    [Fact]
    public async Task Time_zone_preserves_every_country_code_in_a_shared_iana_row()
    {
        var catalog = new JsonReferenceDataCatalog();
        var zones = await ((ITimeZoneCatalog)catalog).ListAsync(TestContext.Current.CancellationToken);
        var shared = zones.FirstOrDefault(zone => zone.CountryCodes.Count > 1);
        Assert.NotNull(shared);
        var (service, _) = CreateService();

        var result = await service.GetTimeZonesAsync("es-AR", shared.Id, TestContext.Current.CancellationToken);

        var returned = Assert.Single(result.Value);
        Assert.Equal(shared.CountryCodes, returned.CountryCodes);
        Assert.Equal(shared.IsEnabled, returned.IsEnabled);
    }

    [Fact]
    public async Task Translation_uses_the_requested_cultures_fallback_before_the_default()
    {
        var data = new FallbackCatalog();
        var otherCatalogs = new JsonReferenceDataCatalog();
        var service = new ReferenceDataService(
            data, otherCatalogs, otherCatalogs, data, otherCatalogs,
            new FakeTimeProvider(), new FakeLogger<ReferenceDataService>());

        var result = await service.GetCurrenciesAsync("fr-CA", null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("peso en francés", Assert.Single(result.Value).Name);
        var disabledCulture = await service.GetCulturesAsync("es-AR", "fr-FR", TestContext.Current.CancellationToken);
        Assert.False(Assert.Single(disabledCulture.Value).IsEnabled);
    }

    [Fact]
    public async Task Search_ignores_accents_in_translated_names()
    {
        var (service, _) = CreateService();

        var result = await service.GetTimeZonesAsync(
            "es-AR", "cordoba", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value, item => item.Id == "America/Argentina/Cordoba" && item.City == "Córdoba");
    }

    [Fact]
    public void Application_registers_the_service_explicitly()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var registration = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IReferenceDataService));
        Assert.Equal(typeof(ReferenceDataService), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
    }

    private static (ReferenceDataService Service, FakeLogger<ReferenceDataService> Logger) CreateService()
    {
        var catalog = new JsonReferenceDataCatalog();
        var logger = new FakeLogger<ReferenceDataService>();
        var service = new ReferenceDataService(
            catalog, catalog, catalog, catalog, catalog, new FakeTimeProvider(), logger);
        return (service, logger);
    }

    private sealed class FallbackCatalog : ICurrencyCatalog, ICultureCatalog
    {
        private static readonly CurrencyCatalogEntry Currency = new(
            "ARS", "032", 2, "$", true, null,
            [
                new("fr-FR", "peso en francés", "pesos en francés", "ARS"),
                new("es-AR", "peso argentino", "pesos argentinos", "$"),
            ]);

        private static readonly IReadOnlyList<CultureCatalogEntry> Cultures =
        [
            new("es-AR", "es", "AR", "dd/MM/yyyy", "HH:mm", "dd/MM/yyyy HH:mm",
                "d MMMM yyyy", ",", ".", "{symbol} {number}", "{number} %",
                null, true, true, null, [new("es-AR", "Español")]),
            new("fr-FR", "fr", "FR", "dd/MM/yyyy", "HH:mm", "dd/MM/yyyy HH:mm",
                "d MMMM yyyy", ",", " ", "{symbol} {number}", "{number} %",
                "es-AR", false, false, null, [new("es-AR", "Francés")]),
            new("fr-CA", "fr", "CA", "dd/MM/yyyy", "HH:mm", "dd/MM/yyyy HH:mm",
                "d MMMM yyyy", ",", " ", "{symbol} {number}", "{number} %",
                "fr-FR", true, false, null, [new("es-AR", "Francés canadiense")]),
        ];

        Task<IReadOnlyList<CurrencyCatalogEntry>> ICurrencyCatalog.ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CurrencyCatalogEntry>>([Currency]);

        Task<CurrencyCatalogEntry?> ICurrencyCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult<CurrencyCatalogEntry?>(code == Currency.Code ? Currency : null);

        Task<IReadOnlyList<CultureCatalogEntry>> ICultureCatalog.ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Cultures);

        Task<CultureCatalogEntry?> ICultureCatalog.FindAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(Cultures.FirstOrDefault(entry => entry.Code == code));
    }

    private sealed class DisabledTaxIdTypeCatalog(ITaxIdTypeCatalog source) : ITaxIdTypeCatalog
    {
        public async Task<IReadOnlyList<TaxIdTypeCatalogEntry>> ListAsync(CancellationToken cancellationToken) =>
            (await source.ListAsync(cancellationToken))
                .Select(entry => entry.Code == "AR-DNI" ? entry with { IsEnabled = false } : entry)
                .ToArray();

        public async Task<TaxIdTypeCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken) =>
            (await ListAsync(cancellationToken)).FirstOrDefault(entry => entry.Code == code);
    }
}
