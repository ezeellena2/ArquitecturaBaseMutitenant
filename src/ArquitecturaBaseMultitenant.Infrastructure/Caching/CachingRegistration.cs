using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

internal static class CachingRegistration
{
    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHybridCache();
        return services;
    }
}
