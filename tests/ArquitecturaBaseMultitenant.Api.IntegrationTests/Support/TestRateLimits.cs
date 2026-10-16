using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Las cuotas de la suite (E9). Todas las pruebas comparten la IP del TestServer y el Redis de la corrida, así que cada host
/// arranca con el tope máximo en los doce perfiles y la configuración que siembra los hereda. Las pruebas de cuotas fijan
/// las suyas en una base y un ambiente de Redis propios.
/// </summary>
internal static class TestRateLimits
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    internal static PlatformRateLimits Unlimited { get; } = new(Max(PlatformRateLimits.Initial.Ip),
        Max(PlatformRateLimits.Initial.PublicIp), Max(PlatformRateLimits.Initial.Identity),
        Max(PlatformRateLimits.Initial.Tenant), Max(PlatformRateLimits.Initial.LoginCode),
        Max(PlatformRateLimits.Initial.LoginVerify), Max(PlatformRateLimits.Initial.InvitationPreview),
        Max(PlatformRateLimits.Initial.InvitationAccept), Max(PlatformRateLimits.Initial.AssistantQuickQuery),
        Max(PlatformRateLimits.Initial.AssistantConversation), Max(PlatformRateLimits.Initial.AssistantNewKey),
        Max(PlatformRateLimits.Initial.WhatsAppWebhook));

    /// <summary>Las opciones de arranque con el tope máximo, para la configuración de un host de prueba.</summary>
    internal static IEnumerable<KeyValuePair<string, string?>> Settings =>
        Enum.GetValues<RateLimitProfile>().Select(profile => KeyValuePair.Create<string, string?>(
            "RateLimiting:" + profile + ":PermitLimit", RateLimitQuota.MaxPermitLimit.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Fija las cuotas guardadas de una base propia por SQL (la crea si falta) y avanza la generación de la configuración,
    /// como pide la ficha de caché para un cambio que no pasa por el guardado.
    /// </summary>
    internal static async Task SetAsync(IServiceProvider services, PlatformRateLimits limits, CancellationToken ct)
    {
        await services.SeedDatabaseAsync(ct);
        await using var scope = services.CreateAsyncScope();
        var json = JsonSerializer.Serialize(limits, Web);
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlAsync(
            $"""UPDATE platform."PlatformSettings" SET "RateLimits" = CAST({json} AS jsonb)""", ct);
        await CacheGenerations.AdvanceAsync(scope.ServiceProvider, ct, "p:settings");
    }

    private static RateLimitQuota Max(RateLimitQuota quota) => quota with { PermitLimit = RateLimitQuota.MaxPermitLimit };
}
