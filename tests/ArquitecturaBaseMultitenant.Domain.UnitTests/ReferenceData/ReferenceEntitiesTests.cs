using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.ReferenceData;

/// <summary>
/// Comprueba que los catálogos conserven claves naturales, traducciones y asociaciones oficiales. Protege
/// los datos ausentes sin reemplazarlos por valores inventados.
/// </summary>
public sealed class ReferenceEntitiesTests
{
    [Fact]
    public void Currency_keeps_natural_code_precision_order_and_culture_specific_symbols()
    {
        var currency = Currency.Create("BHD", "048", 3, ".د.ب", isEnabled: false, sortOrder: 4);
        var spanish = CurrencyTranslation.Create("BHD", "es-AR", "dinar bareiní", "dinares bareiníes", "BHD");
        var english = CurrencyTranslation.Create("BHD", "en-US", "Bahraini dinar", "Bahraini dinars", "BHD");

        Assert.Equal("BHD", currency.Code);
        Assert.Equal("048", currency.NumericCode);
        Assert.Equal(3, currency.MinorUnits);
        Assert.Equal(".د.ب", currency.Symbol);
        Assert.False(currency.IsEnabled);
        Assert.Equal(4, currency.SortOrder);
        Assert.Equal(("BHD", "es-AR"), (spanish.CurrencyCode, spanish.Culture));
        Assert.Equal("dinares bareiníes", spanish.NamePlural);
        Assert.Equal("BHD", spanish.DisplaySymbol);
        Assert.Equal(("BHD", "en-US"), (english.CurrencyCode, english.Culture));
    }

    [Fact]
    public void Country_preserves_missing_official_data_without_substitutes()
    {
        var country = Country.Create("AQ", "ATA", "010", null, null, null, isEnabled: false, sortOrder: null);
        var translation = CountryTranslation.Create("AQ", "es-AR", "Antártida");

        Assert.Equal("AQ", country.Code);
        Assert.Equal("ATA", country.Alpha3);
        Assert.Equal("010", country.NumericCode);
        Assert.Null(country.CallingCode);
        Assert.Null(country.DefaultCurrencyCode);
        Assert.Null(country.DefaultTimeZoneId);
        Assert.False(country.IsEnabled);
        Assert.Null(country.SortOrder);
        Assert.Equal(("AQ", "es-AR"), (translation.CountryCode, translation.Culture));
        Assert.Equal("Antártida", translation.Name);
    }

    [Fact]
    public void Time_zone_keeps_each_country_association_and_retired_associations()
    {
        const string zoneId = "America/Argentina/Buenos_Aires";
        var zone = ReferenceTimeZone.Create(zoneId, isEnabled: true, sortOrder: 1);
        var first = TimeZoneCountry.Create(zoneId, "AR", isEnabled: true);
        var second = TimeZoneCountry.Create(zoneId, "UY", isEnabled: false);
        var translation = TimeZoneTranslation.Create(zoneId, "es-AR", "Buenos Aires");
        var utc = ReferenceTimeZone.Create("UTC", isEnabled: true, sortOrder: null);

        Assert.Equal(zoneId, zone.Id);
        Assert.True(zone.IsEnabled);
        Assert.Equal(1, zone.SortOrder);
        Assert.Equal((zoneId, "AR"), (first.TimeZoneId, first.CountryCode));
        Assert.Equal((zoneId, "UY"), (second.TimeZoneId, second.CountryCode));
        Assert.True(first.IsEnabled);
        Assert.False(second.IsEnabled);
        Assert.Equal((zoneId, "es-AR"), (translation.TimeZoneId, translation.Culture));
        Assert.Equal("Buenos Aires", translation.City);
        Assert.Equal("UTC", utc.Id);
        Assert.Null(typeof(ReferenceTimeZone).GetProperty("CountryCode"));
    }

    [Fact]
    public void Culture_keeps_format_profile_fallback_default_and_display_culture_translation()
    {
        var culture = Culture.Create("es-AR", "es", "AR", "dd/MM/yyyy", "HH:mm",
            "dd/MM/yyyy HH:mm", "d 'de' MMMM 'de' yyyy", "a. m.", "p. m.",
            ",", ".", "{symbol} {number}",
            "{number} %", null, isEnabled: true, isDefault: true, sortOrder: 1);
        var translation = CultureTranslation.Create("es-AR", "en-US", "Spanish (Argentina)");

        Assert.Equal("es-AR", culture.Code);
        Assert.Equal("es", culture.LanguageCode);
        Assert.Equal("AR", culture.CountryCode);
        Assert.Equal("dd/MM/yyyy", culture.DatePattern);
        Assert.Equal("HH:mm", culture.TimePattern);
        Assert.Equal("dd/MM/yyyy HH:mm", culture.DateTimePattern);
        Assert.Equal("d 'de' MMMM 'de' yyyy", culture.LongDatePattern);
        Assert.Equal("a. m.", culture.AmDesignator);
        Assert.Equal("p. m.", culture.PmDesignator);
        Assert.Equal(",", culture.DecimalSeparator);
        Assert.Equal(".", culture.GroupSeparator);
        Assert.Equal("{symbol} {number}", culture.CurrencyPattern);
        Assert.Equal("{number} %", culture.PercentPattern);
        Assert.Null(culture.FallbackCulture);
        Assert.True(culture.IsEnabled);
        Assert.True(culture.IsDefault);
        Assert.Equal(1, culture.SortOrder);
        Assert.Equal(("es-AR", "en-US"), (translation.CultureCode, translation.DisplayCulture));
        Assert.Equal("Spanish (Argentina)", translation.Name);
    }

    [Fact]
    public void Tax_id_type_keeps_validator_and_translation_without_hardcoded_country_rules()
    {
        var type = TaxIdType.Create("AR-CUIT", "AR", "CUIT", "99-99999999-9",
            "ar-cuit-mod11", "Both", isEnabled: true, sortOrder: 2);
        var translation = TaxIdTypeTranslation.Create("AR-CUIT", "en-US", "CUIT");

        Assert.Equal("AR-CUIT", type.Code);
        Assert.Equal("AR", type.CountryCode);
        Assert.Equal("CUIT", type.Label);
        Assert.Equal("99-99999999-9", type.Mask);
        Assert.Equal("ar-cuit-mod11", type.ValidatorKey);
        Assert.Equal("Both", type.AppliesTo);
        Assert.True(type.IsEnabled);
        Assert.Equal(2, type.SortOrder);
        Assert.Equal(("AR-CUIT", "en-US"), (translation.TaxIdTypeCode, translation.Culture));
        Assert.Equal("CUIT", translation.Name);
    }

    [Fact]
    public void Global_reference_entities_have_natural_keys_and_no_rls_marker()
    {
        Type[] referenceTypes =
        [
            typeof(Currency), typeof(CurrencyTranslation), typeof(Country), typeof(CountryTranslation),
            typeof(ReferenceTimeZone), typeof(TimeZoneCountry), typeof(TimeZoneTranslation),
            typeof(Culture), typeof(CultureTranslation), typeof(TaxIdType), typeof(TaxIdTypeTranslation),
        ];

        Assert.All(referenceTypes, type =>
        {
            Assert.False(typeof(Entity).IsAssignableFrom(type));
            Assert.False(typeof(ITenantOwned).IsAssignableFrom(type));
            Assert.False(typeof(IPublishedByBusiness).IsAssignableFrom(type));
            Assert.False(typeof(IConsumerBusinessShared).IsAssignableFrom(type));
            Assert.All(type.GetProperties(), property => Assert.Null(property.GetSetMethod()));
        });
    }
}
