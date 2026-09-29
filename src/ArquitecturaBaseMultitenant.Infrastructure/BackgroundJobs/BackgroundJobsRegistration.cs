using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

internal static class BackgroundJobsRegistration
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<TenantJobRunner>();
        return services;
    }
}
