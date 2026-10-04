using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>Registra los roles y los permisos efectivos.</summary>
internal static class RolesRegistration
{
    internal static IServiceCollection AddRoles(this IServiceCollection services)
    {
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<RoleAssignmentGuard>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IPlatformPermissionService, PlatformPermissionService>();
        return services;
    }
}
