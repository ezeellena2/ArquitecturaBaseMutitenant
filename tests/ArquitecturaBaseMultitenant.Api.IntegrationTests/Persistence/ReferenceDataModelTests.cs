using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class ReferenceDataModelTests
{
    [Fact]
    public void All_eleven_reference_tables_use_platform_and_natural_keys_without_rls()
    {
        using var context = CreateContext();
        var model = context.Model;
        var expected = new (Type Type, string Table, string[] Key)[]
        {
            (typeof(Currency), "Currencies", ["Code"]),
            (typeof(CurrencyTranslation), "CurrencyTranslations", ["CurrencyCode", "Culture"]),
            (typeof(Country), "Countries", ["Code"]),
            (typeof(CountryTranslation), "CountryTranslations", ["CountryCode", "Culture"]),
            (typeof(ReferenceTimeZone), "TimeZones", ["Id"]),
            (typeof(TimeZoneCountry), "TimeZoneCountries", ["TimeZoneId", "CountryCode"]),
            (typeof(TimeZoneTranslation), "TimeZoneTranslations", ["TimeZoneId", "Culture"]),
            (typeof(Culture), "Cultures", ["Code"]),
            (typeof(CultureTranslation), "CultureTranslations", ["CultureCode", "DisplayCulture"]),
            (typeof(TaxIdType), "TaxIdTypes", ["Code"]),
            (typeof(TaxIdTypeTranslation), "TaxIdTypeTranslations", ["TaxIdTypeCode", "Culture"]),
        };

        foreach (var item in expected)
        {
            var entity = model.FindEntityType(item.Type);
            Assert.NotNull(entity);
            Assert.Equal("platform", entity.GetSchema());
            Assert.Equal(item.Table, entity.GetTableName());
            Assert.Equal(item.Key, entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
            Assert.Empty(entity.GetDeclaredQueryFilters());
        }

        Assert.Equal(expected.Length, model.GetEntityTypes().Count());
    }

    [Fact]
    public void References_and_translations_have_foreign_keys_to_their_catalog_rows()
    {
        using var context = CreateContext();
        var model = context.Model;

        AssertForeignKey<CurrencyTranslation, Currency>(model, nameof(CurrencyTranslation.CurrencyCode));
        AssertForeignKey<CurrencyTranslation, Culture>(model, nameof(CurrencyTranslation.Culture));
        AssertForeignKey<CountryTranslation, Country>(model, nameof(CountryTranslation.CountryCode));
        AssertForeignKey<CountryTranslation, Culture>(model, nameof(CountryTranslation.Culture));
        AssertForeignKey<TimeZoneTranslation, ReferenceTimeZone>(model, nameof(TimeZoneTranslation.TimeZoneId));
        AssertForeignKey<TimeZoneTranslation, Culture>(model, nameof(TimeZoneTranslation.Culture));
        AssertForeignKey<CultureTranslation, Culture>(model, nameof(CultureTranslation.CultureCode));
        AssertForeignKey<CultureTranslation, Culture>(model, nameof(CultureTranslation.DisplayCulture));
        AssertForeignKey<TaxIdTypeTranslation, TaxIdType>(model, nameof(TaxIdTypeTranslation.TaxIdTypeCode));
        AssertForeignKey<TaxIdTypeTranslation, Culture>(model, nameof(TaxIdTypeTranslation.Culture));
        AssertForeignKey<TimeZoneCountry, ReferenceTimeZone>(model, nameof(TimeZoneCountry.TimeZoneId));
        AssertForeignKey<TimeZoneCountry, Country>(model, nameof(TimeZoneCountry.CountryCode));
        AssertForeignKey<Culture, Country>(model, nameof(Culture.CountryCode));
        AssertForeignKey<Culture, Culture>(model, nameof(Culture.FallbackCulture), isRequired: false);
        AssertForeignKey<TaxIdType, Country>(model, nameof(TaxIdType.CountryCode));
        AssertForeignKey<Country, Currency>(model, nameof(Country.DefaultCurrencyCode), isRequired: false);
        AssertForeignKey<Country, ReferenceTimeZone>(model, nameof(Country.DefaultTimeZoneId), isRequired: false);
    }

    [Fact]
    public void Source_optional_values_remain_nullable_and_iso_codes_keep_their_width()
    {
        using var context = CreateContext();
        var model = context.Model;

        var currencies = model.FindEntityType(typeof(Currency))!;
        var countries = model.FindEntityType(typeof(Country))!;
        var zones = model.FindEntityType(typeof(ReferenceTimeZone))!;
        var cultures = model.FindEntityType(typeof(Culture))!;

        Assert.Equal("character(3)", currencies.FindProperty(nameof(Currency.Code))!.GetColumnType());
        Assert.Equal("character(2)", countries.FindProperty(nameof(Country.Code))!.GetColumnType());
        Assert.True(currencies.FindProperty(nameof(Currency.MinorUnits))!.IsNullable);
        Assert.True(currencies.FindProperty(nameof(Currency.SortOrder))!.IsNullable);
        Assert.True(countries.FindProperty(nameof(Country.CallingCode))!.IsNullable);
        Assert.True(countries.FindProperty(nameof(Country.DefaultCurrencyCode))!.IsNullable);
        Assert.True(countries.FindProperty(nameof(Country.DefaultTimeZoneId))!.IsNullable);
        Assert.True(zones.FindProperty(nameof(ReferenceTimeZone.SortOrder))!.IsNullable);
        Assert.True(cultures.FindProperty(nameof(Culture.FallbackCulture))!.IsNullable);
        Assert.True(cultures.FindProperty(nameof(Culture.SortOrder))!.IsNullable);
        Assert.All(new[] { typeof(Currency), typeof(Country), typeof(ReferenceTimeZone), typeof(Culture), typeof(TaxIdType), typeof(TimeZoneCountry) },
            type => Assert.False(model.FindEntityType(type)!.FindProperty("IsEnabled")!.IsNullable));
    }

    private static void AssertForeignKey<TEntity, TPrincipal>(IModel model, string propertyName, bool isRequired = true)
    {
        var entity = model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entity);
        var foreignKey = Assert.Single(entity.GetForeignKeys(), key =>
            key.PrincipalEntityType.ClrType == typeof(TPrincipal)
            && key.Properties.Select(property => property.Name).SequenceEqual([propertyName]));
        Assert.Equal(isRequired, foreignKey.IsRequired);
    }

    private static ReferenceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ReferenceContext>()
            .UseNpgsql("Host=localhost;Database=reference_model_test")
            .Options;
        return new ReferenceContext(options);
    }

    private sealed class ReferenceContext(DbContextOptions<ReferenceContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly,
                type => type.Namespace?.EndsWith(".Configurations.Platform.ReferenceData", StringComparison.Ordinal) == true);
        }
    }
}
