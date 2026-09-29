using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

internal static class CachingRegistration
{
    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHybridCache();
        services.AddSingleton<ReferenceDataCache>();
        services.AddSingleton<ITenantStatusCache, TenantStatusCache>();
        return services;
    }
}
