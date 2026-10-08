using ArquitecturaBaseMultitenant.Api.Features;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp;

/// <summary>Esqueleto exportable de T01; la composición por capa y las opciones se completan en T02.</summary>
public static class WhatsAppApiModule
{
    public const string WebhookRoute = "WhatsApp.Webhook";
    public const string WebhookRateLimit = "WhatsAppWebhook";

    public static IServiceCollection AddWhatsAppModule(this IServiceCollection services)
    {
        services.AddSingleton<IInstalledModuleRouteAvailability, PendingWebhookAvailability>();
        services.AddSingleton<ArquitecturaBaseMultitenant.Api.OpenApi.IHttpContractContributor, WhatsAppContractContributor>();
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(WebhookRateLimit, context =>
            RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true,
                })));
        return services;
    }

    private sealed class PendingWebhookAvailability : IInstalledModuleRouteAvailability
    {
        public string Key => WebhookRoute;
        // T01 no valida secretos ni implementa firma: nunca deja ejecutar un webhook real.
        public bool IsAvailable => false;
    }
}
