using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>Registra el ejecutor que abre un scope DI y entra al tenant para un trabajo en segundo plano. Los workers lo usan antes de tocar datos privados.</summary>
internal static class BackgroundJobsRegistration
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<TenantJobRunner>();
        return services;
    }
}
