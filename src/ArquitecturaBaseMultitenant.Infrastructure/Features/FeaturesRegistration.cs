using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace ArquitecturaBaseMultitenant.Infrastructure.Features;

/// <summary>Registra el filtro scoped y conserva el apagado de emergencia de configuración.</summary>
internal static class FeaturesRegistration
{
    public static IServiceCollection AddFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        var defaults = Domain.Features.Features.All.ToDictionary(
            definition => $"{definition.Key}:EnabledFor:0:Name", _ => (string?)"Tenant", StringComparer.Ordinal);
        var flags = new ConfigurationBuilder().AddInMemoryCollection(defaults)
            .AddConfiguration(configuration.GetSection("FeatureManagement")).Build();
        services.AddSingleton<IFeatureCatalog, FeatureCatalog>();
        services.AddScoped<IFeatureService, FeatureService>();
        services.AddScopedFeatureManagement(flags).AddFeatureFilter<TenantFeatureFilter>();
        return services;
    }
}
