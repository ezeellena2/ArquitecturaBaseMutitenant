namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Resuelve permisos efectivos desde las asignaciones de la organización activa.</summary>
public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId,
        CancellationToken cancellationToken);

    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken);

    Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission,
        CancellationToken cancellationToken);

    Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken);
}
