namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>
/// Los permisos que dan los roles de una persona en la organización Business activa, cacheados en Redis con las
/// generaciones de ADR 0052. Los de organización van aparte de los de empresa: un permiso que no se proyecta desde una
/// empresa nunca consulta empresas.
/// </summary>
public interface IRolePermissionCache
{
    /// <summary>Los permisos de sus roles de organización.</summary>
    Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Por cada empresa activa donde tiene la membresía activa, los permisos de sus roles de esa empresa; vacío si no
    /// tiene roles propios ahí.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetCompanyPermissionsAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken);
}
