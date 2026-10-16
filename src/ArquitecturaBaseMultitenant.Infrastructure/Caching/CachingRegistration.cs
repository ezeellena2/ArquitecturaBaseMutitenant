using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Registra la caché compartida de ADR 0052: Redis como única caché detrás de HybridCache, las generaciones durables de
/// sus etiquetas y las cachés del inventario. Cada implementación usa claves con el alcance definido en CacheKeys.
/// </summary>
internal static class CachingRegistration
{
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        // El exportador OpenAPI arranca el host sin las conexiones de Aspire ni atiende pedidos: va sin Redis.
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is not "GetDocument.Insider")
            services.AddRedis(configuration);
        return services.AddCacheGenerations();
    }

    /// <summary>
    /// Las generaciones y las cachés sin Redis; también las registra la persistencia, porque el DbContext usa sus
    /// interceptores. El seed técnico, con su propio proveedor, avanza igual las generaciones de lo que cambia; sin
    /// Redis, cada lectura va a PostgreSQL. Se puede llamar más de una vez.
    /// </summary>
    internal static IServiceCollection AddCacheGenerations(this IServiceCollection services)
    {
        if (services.Any(service => service.ServiceType == typeof(SharedCache))) return services;
        services.AddMetrics();
        services.AddHybridCache(options => options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Flags = HybridCacheEntryFlags.DisableLocalCache,
        });
        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<SharedCache>();
        services.AddSingleton<CacheTagInvalidator>();
        services.AddSingleton(provider => new Lazy<CacheTagInvalidator>(provider.GetRequiredService<CacheTagInvalidator>));
        services.AddScoped<PendingCacheTags>();
        services.AddScoped<CacheTagCollector>();
        services.AddScoped<CacheGenerationInterceptor>();
        services.AddSingleton<ITenantStatusCache, TenantStatusCache>();
        services.AddSingleton<ITenantFeatureCache, TenantFeatureCache>();
        services.AddSingleton<IAccessStatusCache, AccessStatusCache>();
        services.AddScoped<IRolePermissionCache, RolePermissionCache>();
        // Sin Redis, cada admisión de cuotas cuenta en PostgreSQL (E9).
        services.TryAddSingleton<IRateLimitCounter, DurableOnlyRateLimitCounter>();
        return services;
    }

    private static void AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.SectionName))
            .Configure(options => options.ConnectionString =
                configuration.GetConnectionString("cache") ?? options.ConnectionString)
            .PostConfigure<IHostEnvironment>((options, environment) =>
            {
                if (string.IsNullOrWhiteSpace(options.Environment)) options.Environment = environment.EnvironmentName;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<RedisConnection>();
        services.AddSingleton<IRateLimitCounter, RedisRateLimitCounter>();
        services.AddSingleton(provider => new RedisCache(Options.Create(new RedisCacheOptions
        {
            InstanceName = provider.GetRequiredService<IOptions<CacheOptions>>().Value.KeyPrefix,
            ConnectionMultiplexerFactory = provider.GetRequiredService<RedisConnection>().GetAsync,
        })));
        services.AddSingleton<IDistributedCache>(provider => new ResilientDistributedCache(
            provider.GetRequiredService<RedisCache>(), provider.GetRequiredService<CacheMetrics>(),
            provider.GetRequiredService<ILogger<ResilientDistributedCache>>()));
        // Sin la etiqueta de /alive: la caché es readiness, no vida del proceso.
        services.AddHealthChecks().AddCheck<RedisHealthCheck>("cache");
    }
}
