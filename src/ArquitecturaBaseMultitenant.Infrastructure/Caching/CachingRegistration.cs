using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>Registra HybridCache y las cachés de referencias y estados. Cada implementación usa claves con el alcance definido en CacheKeys.</summary>
internal static class CachingRegistration
{
    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHybridCache();
        services.AddSingleton<ReferenceDataCache>();
        services.AddSingleton<ITenantStatusCache, TenantStatusCache>();
        services.AddSingleton<IAccessStatusCache, AccessStatusCache>();
        return services;
    }
}
