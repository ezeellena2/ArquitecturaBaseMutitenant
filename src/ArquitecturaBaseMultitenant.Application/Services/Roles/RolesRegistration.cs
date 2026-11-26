using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>Registra los roles y los permisos efectivos, con sus acciones (ADR 0068).</summary>
internal static class RolesRegistration
{
    internal static IServiceCollection AddRoles(this IServiceCollection services)
    {
        services.AddScoped<IAssistantActionSource, RoleActionSource>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<RoleAssignmentGuard>();
        services.AddScoped<DelegationPermissionsResolver>();
        services.AddScoped<OrganizationDelegationGuard>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IPlatformPermissionService, PlatformPermissionService>();
        return services;
    }
}
