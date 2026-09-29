using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class ReferenceDataSeederTests(ApiFactory factory)
{
    [Fact]
    public async Task Bootstrap_populates_the_official_reference_catalogs()
    {
        using var client = factory.CreateClient();
        await using var context = CreateContext();
        var source = new JsonReferenceDataCatalog();
        var snapshot = await ReferenceDataSeedSnapshot.LoadAsync(source, TestContext.Current.CancellationToken);

        Assert.Equal(snapshot.Currencies.Count, await CountAsync<Currency>(context));
        Assert.Equal(snapshot.Countries.Count, await CountAsync<Country>(context));
        Assert.Equal(snapshot.TimeZones.Count, await CountAsync<ReferenceTimeZone>(context));
        Assert.Equal(snapshot.Cultures.Count, await CountAsync<Culture>(context));
        Assert.Equal(snapshot.TaxIdTypes.Count, await CountAsync<TaxIdType>(context));
        var english = await context.Set<Culture>().SingleAsync(
            item => item.Code == "en-US", TestContext.Current.CancellationToken);
        Assert.Equal("AM", english.AmDesignator);
        Assert.Equal("PM", english.PmDesignator);
    }

    [Fact]
    public async Task Five_official_catalogs_seed_all_tables_and_second_run_changes_nothing()
    {
        using var client = factory.CreateClient();
        await using var context = CreateContext();
        var source = new JsonReferenceDataCatalog();
        var snapshot = await ReferenceDataSeedSnapshot.LoadAsync(source, TestContext.Current.CancellationToken);

        Assert.False(await DatabaseBootstrapExtensions.SeedReferenceDataAsync(
            factory.AdminConnectionString, TestContext.Current.CancellationToken));

        Assert.Equal(snapshot.Currencies.Count, await CountAsync<Currency>(context));
        Assert.Equal(snapshot.Currencies.Sum(item => item.Translations.Count), await CountAsync<CurrencyTranslation>(context));
        Assert.Equal(snapshot.Countries.Count, await CountAsync<Country>(context));
        Assert.Equal(snapshot.Countries.Sum(item => item.Translations.Count), await CountAsync<CountryTranslation>(context));
        Assert.Equal(snapshot.TimeZones.Count, await CountAsync<ReferenceTimeZone>(context));
        Assert.Equal(snapshot.TimeZones.Sum(item => item.CountryCodes.Count), await CountAsync<TimeZoneCountry>(context));
        Assert.Equal(snapshot.TimeZones.Sum(item => item.Translations.Count), await CountAsync<TimeZoneTranslation>(context));
        Assert.Equal(snapshot.Cultures.Count, await CountAsync<Culture>(context));
        Assert.Equal(snapshot.Cultures.Sum(item => item.Translations.Count), await CountAsync<CultureTranslation>(context));
        Assert.Equal(snapshot.TaxIdTypes.Count, await CountAsync<TaxIdType>(context));
        Assert.Equal(snapshot.TaxIdTypes.Sum(item => item.Translations.Count), await CountAsync<TaxIdTypeTranslation>(context));

        var sharedZone = snapshot.TimeZones.First(item => item.CountryCodes.Count > 1);
        var countries = await context.Set<TimeZoneCountry>()
            .Where(link => link.TimeZoneId == sharedZone.Id)
            .Select(link => link.CountryCode)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(sharedZone.CountryCodes.Order(), countries.Order());
        Assert.False(await context.Set<TimeZoneCountry>()
            .AnyAsync(link => link.TimeZoneId == "UTC", TestContext.Current.CancellationToken));

        Assert.False(await DatabaseBootstrapExtensions.SeedReferenceDataAsync(
            factory.AdminConnectionString, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Upsert_changes_translations_and_disables_retired_rows_without_deleting_them()
    {
        using var client = factory.CreateClient();
        await using var provider = PersistenceRegistration.CreateReferenceSeedProvider(factory.AdminConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>();
        await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var source = new JsonReferenceDataCatalog();
        var snapshot = await ReferenceDataSeedSnapshot.LoadAsync(source, TestContext.Current.CancellationToken);

        var retiredCurrency = snapshot.Currencies.First(item => item.IsEnabled);
        var changedCurrency = snapshot.Currencies.First(item =>
            item.Code != retiredCurrency.Code && item.Translations.Count > 0);
        var oldTranslation = changedCurrency.Translations[0];
        var updatedName = oldTranslation.Name + " actualizado";
        var sharedZone = snapshot.TimeZones.First(item => item.CountryCodes.Count > 1);
        var retiredCountryCode = sharedZone.CountryCodes[0];
        var updatedSnapshot = snapshot with
        {
            Currencies = snapshot.Currencies
                .Where(item => item.Code != retiredCurrency.Code)
                .Select(item => item.Code == changedCurrency.Code
                    ? item with
                    {
                        Translations = item.Translations.Select(translation =>
                            translation.Culture == oldTranslation.Culture
                                ? translation with { Name = updatedName }
                                : translation).ToArray()
                    }
                    : item).ToArray(),
            TimeZones = snapshot.TimeZones.Select(item => item.Id == sharedZone.Id
                ? item with { CountryCodes = item.CountryCodes.Where(code => code != retiredCountryCode).ToArray() }
                : item).ToArray(),
        };

        Assert.True(await seeder.StageAsync(updatedSnapshot, TestContext.Current.CancellationToken));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        Assert.False((await context.Set<Currency>().SingleAsync(item => item.Code == retiredCurrency.Code,
            TestContext.Current.CancellationToken)).IsEnabled);
        Assert.Equal(snapshot.Currencies.Count, await CountAsync<Currency>(context));
        Assert.Equal(updatedName, (await context.Set<CurrencyTranslation>().SingleAsync(item =>
            item.CurrencyCode == changedCurrency.Code && item.Culture == oldTranslation.Culture,
            TestContext.Current.CancellationToken)).Name);
        Assert.False((await context.Set<TimeZoneCountry>().SingleAsync(item =>
            item.TimeZoneId == sharedZone.Id && item.CountryCode == retiredCountryCode,
            TestContext.Current.CancellationToken)).IsEnabled);
        Assert.Equal(snapshot.TimeZones.Sum(item => item.CountryCodes.Count), await CountAsync<TimeZoneCountry>(context));
        Assert.False(await seeder.StageAsync(updatedSnapshot, TestContext.Current.CancellationToken));
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(factory.AdminConnectionString)
            .Options;
        return new ApplicationDbContext(options, new EmptyTenantContext());
    }

    private static Task<int> CountAsync<TEntity>(ApplicationDbContext context) where TEntity : class =>
        context.Set<TEntity>().CountAsync(TestContext.Current.CancellationToken);

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
