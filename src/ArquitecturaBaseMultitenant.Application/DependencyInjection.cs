using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.ReferenceData;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<IRequestValidator, RequestValidator>();

        return services;
    }
}
