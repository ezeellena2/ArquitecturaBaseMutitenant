using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

public sealed class WhatsAppCostResolverTests
{
    private static readonly CurrencyCode Usd = CurrencyCode.Create("USD").Value;
    private static readonly CurrencyCode Eur = CurrencyCode.Create("EUR").Value;

    [Fact]
    public async Task A_number_without_its_own_rate_is_estimated_with_the_allowed_country_rate()
    {
        var rates = new Rates(("AR", new Money(0.0618m, Usd)));
        var estimate = await Resolver(rates).EstimateAsync(["+5493415550001", "+5493415550002", "+59899000001"], WhatsAppTemplateCategory.Marketing, CancellationToken.None);
        // Antes, el número de Uruguay (sin tarifa) sumaba cero aunque contaba como destinatario.
        Assert.Equal(new Money(0.19m, Usd), estimate.Cost);
        Assert.Equal("AR", estimate.Country);
    }

    [Fact]
    public async Task A_rate_in_another_currency_is_estimated_with_the_allowed_country_rate()
    {
        var rates = new Rates(("AR", new Money(0.0618m, Usd)), ("UY", new Money(0.5m, Eur)));
        var estimate = await Resolver(rates).EstimateAsync(["+5493415550001", "+59899000001"], WhatsAppTemplateCategory.Marketing, CancellationToken.None);
        Assert.Equal(new Money(0.12m, Usd), estimate.Cost);
    }

    [Fact]
    public async Task A_country_with_its_own_rate_uses_it()
    {
        var rates = new Rates(("AR", new Money(0.0618m, Usd)), ("UY", new Money(0.1m, Usd)));
        var estimate = await Resolver(rates).EstimateAsync(["+5493415550001", "+59899000001", "+59899000002"], WhatsAppTemplateCategory.Marketing, CancellationToken.None);
        Assert.Equal(new Money(0.26m, Usd), estimate.Cost);
        Assert.Equal("UY", estimate.Country);
    }

    private static WhatsAppCostResolver Resolver(Rates rates) =>
        new(rates, new Currencies(), new Settings(), new FakeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero)));

    private sealed class Rates(params (string Country, Money Price)[] prices) : IWhatsAppRateReader
    {
        public Task<Money?> FindAsync(string countryCode, WhatsAppPricingCategory category, DateOnly date, CancellationToken ct) =>
            Task.FromResult(prices.Where(price => price.Country == countryCode).Select(price => (Money?)price.Price).FirstOrDefault());
    }

    private sealed class Currencies : ICurrencyCatalog
    {
        public Task<IReadOnlyList<CurrencyCatalogEntry>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CurrencyCatalogEntry>>([]);
        public Task<CurrencyCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult<CurrencyCatalogEntry?>(new CurrencyCatalogEntry(code, "840", 2, "US$", true, 1, []));
    }

    private sealed class Settings : IWhatsAppChannelSettings
    {
        public bool IsEnabled => true;
        public string AppName => "Prueba";
        public string? PhoneNumberId => null;
        public string? ContactPhone => null;
        public IReadOnlyList<string> Countries => ["AR"];
        public int DailyAuthCodeLimit => 100;
        public string? CountryOf(string e164) => e164.StartsWith("+54", StringComparison.Ordinal) ? "AR" : e164.StartsWith("+598", StringComparison.Ordinal) ? "UY" : null;
        public Result ValidateNewDestination(LoginCodeDestination destination) => Result.Success();
    }
}
