using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class CultureProfilesTests
{
    [Theory]
    [InlineData("en-US", "9:05 pre-noon", "9:05 post-noon")]
    [InlineData("es-AR", "9:05 a. m.", "9:05 p. m.")]
    public async Task Profile_uses_catalog_day_periods_and_fixed_date_time_separators(
        string code, string expectedMorning, string expectedAfternoon)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var profile = await new CultureProfiles(new TestCatalog()).LoadAsync(
                code, TestContext.Current.CancellationToken);

            Assert.IsType<GregorianCalendar>(profile.Culture.DateTimeFormat.Calendar);
            Assert.Equal("/", profile.Culture.DateTimeFormat.DateSeparator);
            Assert.Equal(":", profile.Culture.DateTimeFormat.TimeSeparator);
            Assert.Equal("-", profile.Numbers.NegativeSign);
            Assert.Equal(expectedMorning,
                new DateTime(2026, 9, 27, 9, 5, 0).ToString("h:mm tt", profile.Culture));
            Assert.Equal(expectedAfternoon,
                new DateTime(2026, 9, 27, 21, 5, 0).ToString("h:mm tt", profile.Culture));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task Disabled_requested_and_fallback_cultures_are_not_used()
    {
        var profiles = new CultureProfiles(new TestCatalog());

        var requested = await profiles.LoadAsync("FR-ca", TestContext.Current.CancellationToken);
        var disabled = await profiles.LoadAsync("fr-FR", TestContext.Current.CancellationToken);

        Assert.Equal("fr-CA", requested.Entry.Code);
        Assert.Equal(["fr-CA", "es-AR"], requested.TranslationOrder);
        Assert.Equal("es-AR", disabled.Entry.Code);
    }

    [Fact]
    public async Task Formatting_resources_follow_the_catalog_fallback_culture()
    {
        var profile = await new CultureProfiles(new TestCatalog()).LoadAsync(
            "it-IT", TestContext.Current.CancellationToken);

        Assert.Equal("Yes", FormattingTexts.Get("Boolean.True", profile));
    }

    private sealed class TestCatalog : ICultureCatalog
    {
        private static readonly IReadOnlyList<CultureCatalogEntry> Rows =
        [
            Row("es-AR", "es", null, enabled: true, isDefault: true),
            Row("en-US", "en", "es-AR", enabled: true),
            Row("fr-FR", "fr", "es-AR", enabled: false),
            Row("fr-CA", "fr", "fr-FR", enabled: true),
            Row("it-IT", "it", "en-US", enabled: true),
        ];

        public Task<IReadOnlyList<CultureCatalogEntry>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Rows);

        public Task<CultureCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.FirstOrDefault(row => string.Equals(row.Code, code, StringComparison.OrdinalIgnoreCase)));

        private static CultureCatalogEntry Row(string code, string language, string? fallback,
            bool enabled, bool isDefault = false) => new(
                code, language, "AR", "dd/MM/yyyy", "HH:mm", "dd/MM/yyyy HH:mm",
                "d MMMM yyyy", code == "en-US" ? "pre-noon" : "a. m.",
                code == "en-US" ? "post-noon" : "p. m.",
                ",", ".", "{symbol} {number}", "{number} %",
                fallback, enabled, isDefault, null, []);
    }
}
