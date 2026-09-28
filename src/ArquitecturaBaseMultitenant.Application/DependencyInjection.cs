using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.ReferenceData;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IReferenceDataService, ReferenceDataService>();

        return services;
    }
}
