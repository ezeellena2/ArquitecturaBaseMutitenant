using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Registra la configuración de la organización y sus dominios, con su fuente de «Para resolver».</summary>
internal static class OrganizationSettingsRegistration
{
    internal static IServiceCollection AddOrganizationSettings(this IServiceCollection services)
    {
        services.AddScoped<IPendingItemSource, OrganizationDomainPendingItems>();
        services.AddScoped<IOrganizationSettingsService, OrganizationSettingsService>();
        services.AddScoped<IOrganizationDomainService, OrganizationDomainService>();
        services.AddScoped<OrganizationDomainRegistrar>();
        services.AddScoped<OrganizationDomainVerifier>();
        services.AddScoped<OrganizationDomainRemover>();
        services.AddScoped<LoginMethodDomainAssigner>();
        return services;
    }
}
