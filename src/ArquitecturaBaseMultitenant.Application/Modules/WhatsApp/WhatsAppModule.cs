using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp;

/// <summary>Registra casos de uso y adaptadores a los puertos neutrales del núcleo.</summary>
public static class WhatsAppModule
{
    public static IServiceCollection AddWhatsAppModule(this IServiceCollection services)
    {
        services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
        services.AddScoped<WhatsAppWebhookWriter>();
        services.AddScoped<IWhatsAppInboundService, WhatsAppInboundService>();
        services.AddScoped<WhatsAppInboundGuard>();
        services.AddScoped<ILoginCodeAdmission, WhatsAppCodeQuotaIssuer>();
        services.AddScoped<ILoginCodeChannel, WhatsAppLoginCodeChannel>();
        services.AddScoped<IInvitationChannel, WhatsAppInvitationChannel>();
        services.AddScoped<IAccountNoticeChannel, WhatsAppAccountNoticeChannel>();
        services.AddScoped<IOutboxDeliveryObserver, WhatsAppDeliveryObserver>();
        services.AddSingleton<IErrorTextSource, WhatsAppErrorTextSource>();
        return services;
    }
}
