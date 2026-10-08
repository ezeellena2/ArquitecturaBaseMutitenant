using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using Microsoft.AspNetCore.RateLimiting;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp;

/// <summary>Compone contratos/rutas de la capa Api; la disponibilidad viene del puerto del módulo.</summary>
public static class WhatsAppApiModule
{
    public const string WebhookRoute = "WhatsApp.Webhook";
    public const string WebhookRateLimit = "WhatsAppWebhook";

    public static IServiceCollection AddWhatsAppModule(this IServiceCollection services)
    {
        services.AddScoped<Filters.WhatsAppWebhookFilter>();
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.AddService<Filters.WhatsAppWebhookFilter>(order: -3000));
        services.AddSingleton<IInstalledModuleRouteAvailability, WebhookAvailability>();
        services.AddSingleton<ArquitecturaBaseMultitenant.Api.OpenApi.IHttpContractContributor, WhatsAppContractContributor>();
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(WebhookRateLimit, new RateLimiting.WhatsAppWebhookRateLimitPolicy()));
        return services;
    }

    private sealed class WebhookAvailability(IWhatsAppAvailability availability) : IInstalledModuleRouteAvailability
    {
        public string Key => WebhookRoute;
        public bool IsAvailable => availability.IsWebhookEnabled;
    }
}
