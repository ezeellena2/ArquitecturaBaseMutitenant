using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Home;

/// <summary>Registra los inicios de la organización y del miembro.</summary>
internal static class HomeRegistration
{
    internal static IServiceCollection AddHome(this IServiceCollection services)
    {
        services.AddScoped<IHomeService, HomeService>();
        services.AddScoped<OrganizationHomeAreasBuilder>();
        services.AddScoped<MemberHomeCompaniesBuilder>();
        return services;
    }
}
