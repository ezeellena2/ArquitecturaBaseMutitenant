using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp;

/// <summary>Compone contratos/rutas de la capa Api; la disponibilidad viene del puerto del módulo.</summary>
public static class WhatsAppApiModule
{
    public const string WebhookRoute = "WhatsApp.Webhook";
    public const string WebhookRateLimit = "WhatsAppWebhook";

    public static IServiceCollection AddWhatsAppModule(this IServiceCollection services)
    {
        services.AddSingleton<IInstalledModuleRouteAvailability, WebhookAvailability>();
        services.AddSingleton<ArquitecturaBaseMultitenant.Api.OpenApi.IHttpContractContributor, WhatsAppContractContributor>();
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(WebhookRateLimit, context =>
            RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true,
                })));
        return services;
    }

    private sealed class WebhookAvailability(IWhatsAppAvailability availability) : IInstalledModuleRouteAvailability
    {
        public string Key => WebhookRoute;
        public bool IsAvailable => availability.IsWebhookEnabled;
    }
}
