using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta roles de la organización activa para el detalle y los selectores de autorización.</summary>
public interface IRoleReader
{
    Task<RoleFilterCountsResponse> GetFilterCountsAsync(string? search, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken);

    Task<PagedResult<RoleRow>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken);

    Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken);

    /// <summary>Si ya hay otro rol con ese nombre en la misma empresa, o entre los que no tienen empresa (D3).</summary>
    Task<bool> ExistsByNameAsync(string name, Guid? companyId, Guid? excludedRoleId, CancellationToken cancellationToken);

    Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Los roles de organización asignados a la persona con su miembro activo (E9, ADR 0052). Valen en la organización y
    /// en cada empresa activa.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListOrganizationRoleIdsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Por cada empresa activa donde la persona tiene la membresía activa, los roles de empresa que valen ahí (E9): dos
    /// consultas, sin importar cuántas empresas. Una empresa sin roles propios queda con la lista vacía.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ListCompanyRoleIdsAsync(Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Los permisos de cada rol pedido, en una consulta.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ListPermissionsByRoleAsync(IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken);
}
