using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Messaging;

/// <summary>Registra el despacho del outbox, con sus opciones.</summary>
internal static class MessagingRegistration
{
    internal static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddOptions<OutboxOptions>()
            .BindConfiguration(OutboxOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IOutboxDispatchService, OutboxDispatchService>();
        return services;
    }
}
