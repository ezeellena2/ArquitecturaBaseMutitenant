namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// Resuelve permisos efectivos desde las asignaciones de la organización activa. Un cambio de roles, asignaciones,
/// membresías o empresas avanza las generaciones de su caché en la misma transacción (ADR 0052): nadie invalida a mano.
/// </summary>
public interface IPermissionService
{
    /// <summary>Permisos de roles asignados en la organización, sin proyecciones desde empresas.</summary>
    Task<IReadOnlyList<string>> GetAssignedOrganizationPermissionsAsync(Guid userId, CancellationToken cancellationToken);

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
}
