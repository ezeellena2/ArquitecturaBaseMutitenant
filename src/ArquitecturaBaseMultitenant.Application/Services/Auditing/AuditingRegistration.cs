using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Auditing;

/// <summary>Registra la consulta de la auditoría de la organización.</summary>
internal static class AuditingRegistration
{
    internal static IServiceCollection AddAuditing(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationAuditService, OrganizationAuditService>();
        return services;
    }
}
