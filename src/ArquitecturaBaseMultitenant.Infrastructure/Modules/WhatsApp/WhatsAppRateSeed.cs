using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>
/// Carga las tarifas de Meta del JSON versionado (decisión 9 de la E11): agrega las que faltan y nunca cambia una cargada.
/// Cuando Meta publica una tabla nueva (al empezar un trimestre) se suma otro JSON con su fecha de vigencia.
/// </summary>
internal sealed class WhatsAppRateSeed(ApplicationDbContext context) : IModuleDataSeedContributor
{
    internal const string Resource = "WhatsAppReferenceData.meta-rates-2026-10-01.json";

    public async Task StageAsync(CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var existing = await context.Set<WhatsAppRate>().Select(rate => new { rate.CountryCode, rate.Category, rate.EffectiveFrom }).ToListAsync(cancellationToken);
        foreach (var rate in Load())
            if (!existing.Any(value => value.CountryCode == rate.CountryCode && value.Category == rate.Category && value.EffectiveFrom == rate.EffectiveFrom))
                context.Add(rate);
    }

    internal static IReadOnlyList<WhatsAppRate> Load()
    {
        using var stream = typeof(WhatsAppRateSeed).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException("The WhatsApp rate table is missing.");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var from = DateOnly.ParseExact(root.GetProperty("effectiveFrom").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var currency = CurrencyCode.Create(root.GetProperty("currency").GetString()).Value;
        return [.. root.GetProperty("rates").EnumerateArray().SelectMany(row => Enum.GetValues<WhatsAppPricingCategory>().Select(category =>
            WhatsAppRate.Create(row.GetProperty("country").GetString()!, category,
                new Money(row.GetProperty(category.ToString().ToLowerInvariant()).GetDecimal(), currency), from)))];
    }
}
