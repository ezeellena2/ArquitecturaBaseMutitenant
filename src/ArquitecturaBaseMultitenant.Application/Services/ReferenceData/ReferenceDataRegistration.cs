using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.ReferenceData;

/// <summary>Registra los datos de referencia.</summary>
internal static class ReferenceDataRegistration
{
    internal static IServiceCollection AddReferenceData(this IServiceCollection services)
    {
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        return services;
    }
}
