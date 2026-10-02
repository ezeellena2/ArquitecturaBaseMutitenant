namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>Cachea el catálogo de permisos de un rol dentro de una organización.</summary>
public interface IRolePermissionCache
{
    Task<IReadOnlyList<string>> GetRolePermissionsAsync(Guid tenantId, Guid roleId,
        CancellationToken cancellationToken);

    Task InvalidateRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);
}
