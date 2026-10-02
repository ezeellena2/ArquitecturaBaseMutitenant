using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>Administra roles tenant-scoped; cada escritura valida antes y usa una única transacción.</summary>
internal sealed class RoleService(IRoleReader roles, IRoleRepository repository, IPermissionService permissions,
    IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider timeProvider,
    ILogger<RoleService> logger) : IRoleService
{
    public Task<Result<PagedResult<RoleResponse>>> ListAsync(ListRolesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListRoles", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await roles.ListAsync(request, cancellationToken);
            return Result.Success(new PagedResult<RoleResponse>([.. page.Items.Select(ToResponse)],
                page.Page, page.PageSize, page.TotalCount));
        });
    }

    public Task<Result<RoleResponse>> GetAsync(Guid roleId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<RoleResponse>(logger, timeProvider, "GetRole", async () =>
        {
            var role = await roles.FindByIdAsync(roleId, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            return ToResponse(role);
        });

    public Task<Result<IReadOnlyList<PermissionGroupResponse>>> GetPermissionsAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "GetPermissions", () =>
        {
            IReadOnlyList<PermissionGroupResponse> groups =
            [
                .. Permissions.All
                    .GroupBy(AreaOf, StringComparer.Ordinal)
                    .Select(group => new PermissionGroupResponse(group.Key, PermissionTexts.Area(group.Key),
                        [.. group.Select(permission => new PermissionItem(permission,
                            PermissionTexts.Permission(permission), PermissionTexts.Description(permission)))])),
            ];
            return Task.FromResult(Result.Success(groups));
        });

    public Task<Result<Guid>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "CreateRole", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var roleResult = Role.Create(request.Name, request.Description, request.Scope!.Value, request.CompanyId,
                NormalizePermissions(request.Permissions!));
            if (roleResult.IsFailure) return roleResult.Error;

            return await unitOfWork.ExecuteInTransactionAsync(
                ct => CreateCoreAsync(roleResult.Value, ct), CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    public Task<Result> UpdateAsync(UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "UpdateRole", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var result = await unitOfWork.ExecuteInTransactionAsync(
                ct => UpdateCoreAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
            if (result.IsSuccess)
            {
                await permissions.InvalidateRoleAsync(request.RoleId, cancellationToken);
            }

            return result;
        });
    }

    public Task<Result> DeleteAsync(DeleteRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "DeleteRole", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var result = await unitOfWork.ExecuteInTransactionAsync(
                ct => DeleteCoreAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
            if (result.IsSuccess)
            {
                await permissions.InvalidateRoleAsync(request.RoleId, cancellationToken);
            }

            return result;
        });
    }

    private async Task<Result<Guid>> CreateCoreAsync(Role role, CancellationToken cancellationToken)
    {
        if (await roles.ExistsByNameAsync(role.Name, excludedRoleId: null, cancellationToken))
            return RoleErrors.AlreadyExists;
        repository.Add(role);
        return role.Id;
    }

    private async Task<Result> UpdateCoreAsync(UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await repository.GetByIdAsync(request.RoleId, request.Version!.Value, cancellationToken);
        if (role is null) return RoleErrors.NotFound;
        var updated = role.Update(request.Name, request.Description, request.Scope!.Value, request.CompanyId,
            NormalizePermissions(request.Permissions!));
        if (updated.IsFailure) return updated;
        if (await roles.ExistsByNameAsync(role.Name, role.Id, cancellationToken)) return RoleErrors.AlreadyExists;
        repository.MarkUpdated(role);
        return Result.Success();
    }

    private async Task<Result> DeleteCoreAsync(DeleteRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await repository.GetByIdAsync(request.RoleId, request.Version!.Value, cancellationToken);
        if (role is null) return RoleErrors.NotFound;
        if (role.IsSystemRole) return RoleErrors.SystemRoleCannotChange;
        var userCount = await roles.CountUsersAsync(role.Id, cancellationToken);
        if (userCount > 0) return RoleErrors.HasUsers(userCount);
        repository.Remove(role);
        return Result.Success();
    }

    private static string[] NormalizePermissions(IEnumerable<string> permissions) =>
        permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static string AreaOf(string permission) =>
        permission[..permission.IndexOf('.', StringComparison.Ordinal)];

    private static RoleResponse ToResponse(RoleRow role) => new(role.Id, RoleTexts.Name(role.Name), role.Description,
        role.Scope, role.CompanyId, role.IsSystemRole, role.UserCount, role.Permissions,
        role.Version, role.CreatedAtUtc);
}
