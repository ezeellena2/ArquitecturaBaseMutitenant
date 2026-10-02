using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta roles de la organización activa para el detalle y los selectores de autorización.</summary>
public interface IRoleReader
{
    Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken);

    Task<PagedResult<RoleRow>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken);

    Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string name, Guid? excludedRoleId, CancellationToken cancellationToken);

    Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListRoleIdsForUserAsync(Guid userId, Guid? companyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListPermissionsForRoleAsync(Guid roleId, CancellationToken cancellationToken);
}
