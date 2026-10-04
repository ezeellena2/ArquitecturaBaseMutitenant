using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IRoleService
{
    Task<Result<PagedResult<RoleResponse>>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Los roles que se pueden asignar en una empresa: los que valen en cada empresa y los propios de esa empresa.
    /// Lo pide quien ve los miembros de la empresa, sin permisos de roles de la organización.
    /// </summary>
    Task<Result<PagedResult<RoleResponse>>> ListForCompanyAsync(Guid companyId, ListRolesRequest request,
        CancellationToken cancellationToken);

    Task<Result<RoleResponse>> GetAsync(Guid roleId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PermissionGroupResponse>>> GetPermissionsAsync(CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(UpdateRoleRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(DeleteRoleRequest request, CancellationToken cancellationToken);
}
