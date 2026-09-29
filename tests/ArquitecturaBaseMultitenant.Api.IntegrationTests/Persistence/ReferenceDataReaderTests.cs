using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class ReferenceDataReaderTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Five_catalogs_are_composed_from_one_database_reader()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();

        var currencies = scope.ServiceProvider.GetRequiredService<ICurrencyCatalog>();
        Assert.IsType<ReferenceDataReader>(currencies);
        Assert.Same(currencies, scope.ServiceProvider.GetRequiredService<ICountryCatalog>());
        Assert.Same(currencies, scope.ServiceProvider.GetRequiredService<ITimeZoneCatalog>());
        Assert.Same(currencies, scope.ServiceProvider.GetRequiredService<ICultureCatalog>());
        Assert.Same(currencies, scope.ServiceProvider.GetRequiredService<ITaxIdTypeCatalog>());
    }

    [Fact]
    public async Task Reader_returns_every_seeded_row_including_disabled_rows_and_translations()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var source = new JsonReferenceDataCatalog();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var currencyRows = await scope.ServiceProvider.GetRequiredService<ICurrencyCatalog>().ListAsync(Ct);
        var countryRows = await scope.ServiceProvider.GetRequiredService<ICountryCatalog>().ListAsync(Ct);
        var zoneRows = await scope.ServiceProvider.GetRequiredService<ITimeZoneCatalog>().ListAsync(Ct);
        var cultureRows = await scope.ServiceProvider.GetRequiredService<ICultureCatalog>().ListAsync(Ct);
        var taxTypeRows = await scope.ServiceProvider.GetRequiredService<ITaxIdTypeCatalog>().ListAsync(Ct);

        Assert.Equal(await db.Set<Currency>().CountAsync(Ct), currencyRows.Count);
        Assert.Equal(await db.Set<Country>().CountAsync(Ct), countryRows.Count);
        Assert.Equal(await db.Set<ReferenceTimeZone>().CountAsync(Ct), zoneRows.Count);
        Assert.Equal(await db.Set<Culture>().CountAsync(Ct), cultureRows.Count);
        Assert.Equal(await db.Set<TaxIdType>().CountAsync(Ct), taxTypeRows.Count);

        Assert.Equal((await ((ICurrencyCatalog)source).ListAsync(Ct)).Select(row => row.Code).Order(),
            currencyRows.Select(row => row.Code).Order());
        Assert.Equal((await ((ICountryCatalog)source).ListAsync(Ct)).Select(row => row.Code).Order(),
            countryRows.Select(row => row.Code).Order());
        Assert.Equal((await ((ITimeZoneCatalog)source).ListAsync(Ct)).Select(row => row.Id).Order(),
            zoneRows.Select(row => row.Id).Order());
        Assert.Equal((await ((ICultureCatalog)source).ListAsync(Ct)).Select(row => row.Code).Order(),
            cultureRows.Select(row => row.Code).Order());
        Assert.Equal((await ((ITaxIdTypeCatalog)source).ListAsync(Ct)).Select(row => row.Code).Order(),
            taxTypeRows.Select(row => row.Code).Order());

        AssertSameCatalog(await ((ICurrencyCatalog)source).ListAsync(Ct), currencyRows,
            row => row.Code, row => row with
            {
                Translations = row.Translations.OrderBy(translation => translation.Culture).ToArray()
            });
        AssertSameCatalog(await ((ICountryCatalog)source).ListAsync(Ct), countryRows,
            row => row.Code, row => row with
            {
                Translations = row.Translations.OrderBy(translation => translation.Culture).ToArray()
            });
        AssertSameCatalog(await ((ITimeZoneCatalog)source).ListAsync(Ct), zoneRows,
            row => row.Id, row => row with
            {
                CountryCodes = row.CountryCodes.Order().ToArray(),
                Translations = row.Translations.OrderBy(translation => translation.Culture).ToArray()
            });
        AssertSameCatalog(await ((ICultureCatalog)source).ListAsync(Ct), cultureRows,
            row => row.Code, row => row with
            {
                Translations = row.Translations.OrderBy(translation => translation.DisplayCulture).ToArray()
            });
        AssertSameCatalog(await ((ITaxIdTypeCatalog)source).ListAsync(Ct), taxTypeRows,
            row => row.Code, row => row with
            {
                Translations = row.Translations.OrderBy(translation => translation.Culture).ToArray()
            });

        Assert.Contains(currencyRows, row => !row.IsEnabled);
        Assert.Contains(countryRows, row => !row.IsEnabled);
        Assert.Contains(zoneRows, row => !row.IsEnabled);
        var disabledCurrency = currencyRows.First(row => !row.IsEnabled);
        Assert.False((await scope.ServiceProvider.GetRequiredService<ICurrencyCatalog>()
            .FindAsync(disabledCurrency.Code, Ct))!.IsEnabled);
        Assert.All(currencyRows, row => Assert.NotEmpty(row.Translations));
        Assert.All(countryRows, row => Assert.NotEmpty(row.Translations));
        Assert.All(zoneRows, row => Assert.NotEmpty(row.Translations));
        Assert.All(cultureRows, row => Assert.NotEmpty(row.Translations));
        Assert.All(taxTypeRows, row => Assert.NotEmpty(row.Translations));

        var sourceZone = (await ((ITimeZoneCatalog)source).ListAsync(Ct))
            .First(row => row.CountryCodes.Count > 1);
        var actualZone = Assert.Single(zoneRows, row => row.Id == sourceZone.Id);
        Assert.Equal(sourceZone.CountryCodes.Order(), actualZone.CountryCodes.Order());
        Assert.Empty(Assert.Single(zoneRows, row => row.Id == "UTC").CountryCodes);
    }

    [Fact]
    public async Task Retired_zone_country_link_is_hidden_without_hiding_the_zone()
    {
        using var client = factory.CreateClient();
        await using var provider = PersistenceRegistration.CreateReferenceSeedProvider(factory.AdminConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<ITimeZoneCatalog>();
        var cache = scope.ServiceProvider.GetRequiredService<ReferenceDataCache>();
        var seeder = scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>();
        var source = scope.ServiceProvider.GetRequiredService<JsonReferenceDataCatalog>();
        var original = (await ((ITimeZoneCatalog)source).ListAsync(Ct))
            .First(row => row.CountryCodes.Count > 1);
        var retiredCountry = original.CountryCodes[0];

        try
        {
            await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
            await connection.OpenAsync(Ct);
            await using (var command = new NpgsqlCommand(
                "UPDATE platform.\"TimeZoneCountries\" SET \"IsEnabled\" = false " +
                "WHERE \"TimeZoneId\" = @zone AND \"CountryCode\" = @country", connection))
            {
                command.Parameters.AddWithValue("zone", original.Id);
                command.Parameters.AddWithValue("country", retiredCountry);
                Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
            }

            await cache.InvalidateAsync(Ct);
            var actual = await reader.FindAsync(original.Id, Ct);
            Assert.NotNull(actual);
            Assert.Equal(original.IsEnabled, actual.IsEnabled);
            Assert.DoesNotContain(retiredCountry, actual.CountryCodes);
            Assert.Equal(original.CountryCodes.Count - 1, actual.CountryCodes.Count);
        }
        finally
        {
            await seeder.SeedAsync(Ct);
        }
    }

    [Fact]
    public async Task Changed_seed_invalidates_reference_cache_after_commit()
    {
        using var client = factory.CreateClient();
        await using var provider = PersistenceRegistration.CreateReferenceSeedProvider(factory.AdminConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<ICurrencyCatalog>();
        var seeder = scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>();
        var source = scope.ServiceProvider.GetRequiredService<JsonReferenceDataCatalog>();
        var original = (await ((ICurrencyCatalog)source).ListAsync(Ct))[0];
        var changedSymbol = original.Symbol + "-prueba";

        try
        {
            await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
            await connection.OpenAsync(Ct);
            await using (var command = new NpgsqlCommand(
                "UPDATE platform.\"Currencies\" SET \"Symbol\" = @symbol WHERE \"Code\" = @code", connection))
            {
                command.Parameters.AddWithValue("symbol", changedSymbol);
                command.Parameters.AddWithValue("code", original.Code);
                Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
            }

            Assert.Equal(changedSymbol, (await reader.FindAsync(original.Code, Ct))!.Symbol);
            Assert.True(await seeder.SeedAsync(Ct));
            Assert.Equal(original.Symbol, (await reader.FindAsync(original.Code, Ct))!.Symbol);
            Assert.False(await seeder.SeedAsync(Ct));
        }
        finally
        {
            await seeder.SeedAsync(Ct);
        }
    }

    private static void AssertSameCatalog<TEntry>(
        IReadOnlyList<TEntry> expected,
        IReadOnlyList<TEntry> actual,
        Func<TEntry, string> key,
        Func<TEntry, TEntry> normalize)
        where TEntry : notnull
    {
        Assert.Equal(expected.Count, actual.Count);
        var byKey = actual.ToDictionary(key, StringComparer.Ordinal);
        foreach (var sourceRow in expected)
        {
            Assert.True(byKey.TryGetValue(key(sourceRow), out var databaseRow));
            Assert.Equal(JsonSerializer.Serialize(normalize(sourceRow)),
                JsonSerializer.Serialize(normalize(databaseRow)));
        }
    }
}
