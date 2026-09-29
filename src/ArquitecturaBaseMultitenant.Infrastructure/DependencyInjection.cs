using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Identity.OpenIddict;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
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
        services.AddOptions<LoginCodeOptions>()
            .BindConfiguration(LoginCodeOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<LoginCodeHashOptions>()
            .BindConfiguration(LoginCodeHashOptions.SectionName)
            .ValidateDataAnnotations();
        services.AddSingleton<ILoginCodeGenerator, LoginCodeGenerator>();
        services.AddSingleton<ILoginCodeHasher, LoginCodeHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IPayloadProtector, PayloadProtector>();
        services.AddCaching();

        services.AddPersistence(configuration);
        services.AddIdentityServices(configuration);
        // La exportación OpenAPI no atiende requests ni tiene claves de firma o base de datos.
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is not "GetDocument.Insider")
        {
            services.AddOpenIddictServer(configuration, environment);
        }
        services.AddMessaging(configuration, environment);
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
            services.AddSingleton<IIdempotencyStore>(provider => new IdempotencyStore(
                runtimeConnection, provider.GetRequiredService<TimeProvider>()));
            services.AddHostedService<IdempotencyCleanupWorker>();
        }

        return services;
    }
}
