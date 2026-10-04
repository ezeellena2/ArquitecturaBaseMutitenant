namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Resuelve permisos efectivos desde las asignaciones de la organización activa.</summary>
public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Los permisos de cada empresa activa donde la persona es miembro activo y tiene alguno, con los de la
    /// organización incluidos, como <see cref="GetCompanyPermissionsAsync"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId,
        CancellationToken cancellationToken);

    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken);

    Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission,
        CancellationToken cancellationToken);

    Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken);
}
