using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
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

        services.AddSingleton<JsonReferenceDataCatalog>();
        services.AddSingleton<ICurrencyCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());
        services.AddSingleton<ICountryCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());
        services.AddSingleton<ITimeZoneCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());
        services.AddSingleton<ICultureCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());
        services.AddSingleton<ITaxIdTypeCatalog>(provider => provider.GetRequiredService<JsonReferenceDataCatalog>());

        return services;
    }
}
