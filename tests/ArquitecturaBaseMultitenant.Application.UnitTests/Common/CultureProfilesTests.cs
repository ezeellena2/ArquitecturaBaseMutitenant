using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class CultureProfilesTests
{
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
                "d MMMM yyyy", ",", ".", "{symbol} {number}", "{number} %",
                fallback, enabled, isDefault, null, []);
    }
}
