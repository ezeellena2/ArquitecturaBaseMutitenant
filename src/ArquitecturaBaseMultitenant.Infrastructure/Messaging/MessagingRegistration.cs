using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

internal static class MessagingRegistration
{
    internal static IServiceCollection AddMessaging(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<OutboxOptions>()
            .BindConfiguration(OutboxOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IOutbox, Outbox>();
        services.AddScoped<IOutboxDispatchStore, OutboxDispatchStore>();
        // El host de pruebas despacha explícitamente para verificar locks y reintentos sin carreras.
        if (!environment.IsEnvironment("Testing") && configuration.GetConnectionString("appdb") is not null)
        {
            services.AddHostedService<OutboxDispatcher>();
        }
        return services;
    }
}
