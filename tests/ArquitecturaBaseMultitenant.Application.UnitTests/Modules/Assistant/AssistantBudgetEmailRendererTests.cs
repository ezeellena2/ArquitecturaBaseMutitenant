using System.Net;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantBudgetEmailRendererTests
{
    [Theory]
    [InlineData("es-AR", "es", "1.234,50")]
    [InlineData("en-US", "en", "1,234.50")]
    public async Task Template_uses_the_recipients_language_and_catalog_formats_and_escapes_html(string culture, string language, string amount)
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero));
        var formatter = new DisplayFormatter(new MarkupCurrencyCatalog(catalog), catalog, catalog, catalog, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(clock), clock);
        var services = new ServiceCollection();
        services.AddSingleton(formatter);
        ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.AssistantModule.AddAssistantModule(services);
        using var provider = services.BuildServiceProvider();
        var renderer = provider.GetRequiredService<IAssistantBudgetEmailRenderer>();
        var currency = CurrencyCode.Create("USD").Value;
        var email = await renderer.RenderAsync("fixture@example.test", 80, new Money(1234.5m, currency),
            new Money(2000m, currency), culture, "Etc/UTC", TestContext.Current.CancellationToken);
        Assert.Equal("fixture@example.test", email.To);
        Assert.Contains(amount, email.TextBody, StringComparison.Ordinal);
        Assert.Contains("<catalog & symbol>", email.TextBody, StringComparison.Ordinal);
        Assert.Contains($"<html lang=\"{language}\">", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(email.TextBody), email.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<catalog & symbol>", email.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", email.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", email.TextBody, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(email.Subject), email.HtmlBody, StringComparison.Ordinal);
        Assert.NotEmpty(email.Subject);
    }

    private sealed class MarkupCurrencyCatalog(ICurrencyCatalog inner) : ICurrencyCatalog
    {
        public async Task<IReadOnlyList<CurrencyCatalogEntry>> ListAsync(CancellationToken ct) =>
            (await inner.ListAsync(ct)).Select(WithMarkup).ToArray();
        public async Task<CurrencyCatalogEntry?> FindAsync(string code, CancellationToken ct) =>
            await inner.FindAsync(code, ct) is { } row ? WithMarkup(row) : null;
        private static CurrencyCatalogEntry WithMarkup(CurrencyCatalogEntry row) => row with
        {
            Translations = row.Translations.Select(translation => translation with { DisplaySymbol = "<catalog & symbol>" }).ToArray(),
        };
    }
}
