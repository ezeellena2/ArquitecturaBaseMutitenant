using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Messaging;

/// <summary>Registra el despacho del outbox y los topes de correo (ADR 0063), con sus opciones.</summary>
internal static class MessagingRegistration
{
    internal static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddOptions<OutboxOptions>()
            .BindConfiguration(OutboxOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IOutboxDispatchService, OutboxDispatchService>();
        services.AddOptions<EmailQuotaOptions>()
            .BindConfiguration(EmailQuotaOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(quotas => quotas.HasRoomForCodes,
                "Email:Quotas:DailyInvitationReserve must be lower than Email:Quotas:DailyLimit.")
            .ValidateOnStart();
        services.AddScoped<MessageQuotaGuard>();
        services.AddScoped<EmailQuotaGuard>();
        // Es la admisión del canal de correo: LoginCodeIssuer la llama al emitir cada código por correo.
        services.AddScoped<ILoginCodeAdmission>(provider => provider.GetRequiredService<EmailQuotaGuard>());
        return services;
    }
}
