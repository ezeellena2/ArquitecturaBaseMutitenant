using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Notifications;

/// <summary>Registra la campana. Cada área, y cada módulo en su propio registro, suma sus fuentes de avisos.</summary>
internal static class NotificationsRegistration
{
    internal static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
