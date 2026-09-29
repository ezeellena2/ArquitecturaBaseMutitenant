using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITimeZoneService, TimeZoneService>();
        services.AddSingleton<IPhoneNumberDisplayFormatter, LibPhoneNumberDisplayFormatter>();
        services.AddCaching();

        services.AddPersistence(configuration);
        services.AddBackgroundJobs();

        // El exportador construye el host sin PostgreSQL; solo allí usa el JSON que alimenta el seed.
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider")
        {
            services.AddSingleton<JsonReferenceDataCatalog>();
            services.AddSingleton<ICurrencyCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());
            services.AddSingleton<ICountryCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());
            services.AddSingleton<ITimeZoneCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());
            services.AddSingleton<ICultureCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());
            services.AddSingleton<ITaxIdTypeCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());
        }

        // El exportador OpenAPI construye el host sin conexiones de Aspire.
        if (configuration.GetConnectionString("appdb") is { } runtimeConnection)
        {
            services.AddSingleton(provider => new IdempotencyStore(
                runtimeConnection, provider.GetRequiredService<TimeProvider>()));
            services.AddHostedService<IdempotencyCleanupWorker>();
        }

        return services;
    }
}
