using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IRoleService
{
    Task<Result<PagedResult<RoleResponse>>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken);

    Task<Result<RoleResponse>> GetAsync(Guid roleId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PermissionGroupResponse>>> GetPermissionsAsync(CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(UpdateRoleRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(DeleteRoleRequest request, CancellationToken cancellationToken);
}
