using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El costo estimado de mandar una plantilla (decisión 9 de la E11): la tarifa vigente de Meta del país de cada número y de
/// la categoría, sumada y redondeada a los decimales de la moneda. El país que se informa es el de la mayoría; sin números
/// (o si no se reconoce), el primero que habilita WhatsApp.
/// </summary>
internal sealed class WhatsAppCostResolver(IWhatsAppRateReader rates, ICurrencyCatalog currencies, IWhatsAppChannelSettings settings, TimeProvider clock)
{
    internal async Task<WhatsAppCostEstimate> EstimateAsync(IReadOnlyCollection<string> waIds, WhatsAppTemplateCategory category, CancellationToken ct)
    {
        var pricing = Pricing(category);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var fallback = settings.Countries.Count > 0 ? settings.Countries[0] : throw new InvalidOperationException("WhatsApp has no allowed country.");
        var countries = waIds.GroupBy(waId => settings.CountryOf(waId) ?? fallback, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var unit = await rates.FindAsync(fallback, pricing, today, ct)
            ?? throw new InvalidOperationException("The WhatsApp rate table does not cover the allowed country.");
        var total = 0m;
        foreach (var (country, count) in countries)
            if ((country == fallback ? unit : await rates.FindAsync(country, pricing, today, ct)) is { } price && price.Currency == unit.Currency)
                total += price.Amount * count;
        var shown = countries.Count == 0 ? fallback
            : countries.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).First().Key;
        return new WhatsAppCostEstimate(await RoundAsync(new Money(total, unit.Currency), ct), shown);
    }

    /// <summary>Lo que cuesta un mensaje a ese número (o, sin número, en el país habilitado).</summary>
    internal async Task<Money> UnitAsync(string? waId, WhatsAppTemplateCategory category, CancellationToken ct) =>
        waId is null ? await FallbackUnitAsync(category, ct) : (await EstimateAsync([waId], category, ct)).Cost;

    private async Task<Money> FallbackUnitAsync(WhatsAppTemplateCategory category, CancellationToken ct)
    {
        var fallback = settings.Countries.Count > 0 ? settings.Countries[0] : throw new InvalidOperationException("WhatsApp has no allowed country.");
        var unit = await rates.FindAsync(fallback, Pricing(category), DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct)
            ?? throw new InvalidOperationException("The WhatsApp rate table does not cover the allowed country.");
        return await RoundAsync(unit, ct);
    }

    private async Task<Money> RoundAsync(Money money, CancellationToken ct) =>
        (await currencies.FindAsync(money.Currency.Value, ct))?.MinorUnits is { } minorUnits ? money.Round(minorUnits) : money;

    private static WhatsAppPricingCategory Pricing(WhatsAppTemplateCategory category) => category switch
    {
        WhatsAppTemplateCategory.Marketing => WhatsAppPricingCategory.Marketing,
        WhatsAppTemplateCategory.Authentication => WhatsAppPricingCategory.Authentication,
        _ => WhatsAppPricingCategory.Utility,
    };
}

internal sealed record WhatsAppCostEstimate(Money Cost, string Country);
