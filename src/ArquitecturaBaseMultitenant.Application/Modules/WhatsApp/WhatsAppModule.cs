using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp;

/// <summary>Registra solo los esqueletos de su capa; T02 incorpora los casos de uso del módulo.</summary>
public static class WhatsAppModule
{
    public static IServiceCollection AddWhatsAppModule(this IServiceCollection services)
    {
        services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
        return services;
    }
}
