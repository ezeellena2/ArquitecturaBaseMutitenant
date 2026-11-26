using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Administra roles tenant-scoped; cada escritura valida antes y usa una única transacción. Un rol se guarda con lo que
/// incluye cada permiso pedido (decisión 260): lo guardado es lo que permite. Nadie da ni quita un permiso que no tiene
/// (ADR 0061, <see cref="OrganizationDelegationGuard"/>), y los listados dicen qué puede dar quien pide (<c>grantable</c>,
/// decisión 269) con la misma <see cref="OrganizationDelegationPolicy"/> y lo que lee
/// <see cref="DelegationPermissionsResolver"/>.
/// </summary>
internal sealed class RoleService(IRoleReader roles, IRoleRepository repository, ICompanyReader companies,
    DelegationPermissionsResolver delegationPermissions, OrganizationDelegationGuard delegation,
    IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider timeProvider, ILogger<RoleService> logger)
    : IRoleService
{
    public Task<Result<RoleFilterCountsResponse>> GetFilterCountsAsync(string? search, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "GetRoleFilterCounts", async () =>
        {
            if (await validator.ValidateAsync(new ListRolesRequest { Search = search }, cancellationToken) is { } invalid) return invalid;
            return Result.Success(await roles.GetFilterCountsAsync(search, cancellationToken));
        });

    public Task<Result<PagedResult<RoleResponse>>> ListAsync(ListRolesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListRoles", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await roles.ListAsync(request, cancellationToken);
            var held = await delegationPermissions.ReadHeldAsync(cancellationToken);
            return Result.Success(new PagedResult<RoleResponse>(
                [.. page.Items.Select(role => ToResponse(role, held, request.CompanyId))],
                page.Page, page.PageSize, page.TotalCount));
        });
    }

    public Task<Result<PagedResult<RoleResponse>>> ListForCompanyAsync(Guid companyId, ListRolesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListCompanyRoles", async () =>
        {
            var filtered = request with { Scope = null, CompanyId = companyId };
            if (await validator.ValidateAsync(filtered, cancellationToken) is { } invalid) return invalid;
            if (await companies.FindByIdAsync(companyId, cancellationToken) is null) return CompanyErrors.NotFound;
            var page = await roles.ListAsync(filtered, cancellationToken);
            var held = await delegationPermissions.ReadHeldAsync(cancellationToken);
            return Result.Success(new PagedResult<RoleResponse>(
                [.. page.Items.Select(role => ToResponse(role, held, companyId))],
                page.Page, page.PageSize, page.TotalCount));
        });
    }

    public Task<Result<RoleResponse>> GetAsync(Guid roleId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<RoleResponse>(logger, timeProvider, "GetRole", async () =>
        {
            var role = await roles.FindByIdAsync(roleId, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            return ToResponse(role, await delegationPermissions.ReadHeldAsync(cancellationToken), companyId: null);
        });

    public Task<Result<IReadOnlyList<PermissionGroupResponse>>> GetPermissionsAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "GetPermissions", async () =>
        {
            var held = await delegationPermissions.ReadHeldAsync(cancellationToken);
            IReadOnlyList<PermissionGroupResponse> groups =
            [
                .. Permissions.All
                    .GroupBy(AreaOf, StringComparer.Ordinal)
                    .Select(group => new PermissionGroupResponse(group.Key, PermissionTexts.Area(group.Key),
                        // Lo que incluye cada permiso (260) y si quien pide lo puede dar (269 y ADR 0061).
                        [.. group.Select(permission => new PermissionItem(permission,
                            PermissionTexts.Permission(permission), PermissionTexts.Description(permission),
                            Permissions.Inclusions.Of(permission),
                            OrganizationDelegationPolicy.CanGrantPermission(held, permission)))])),
            ];
            return Result.Success(groups);
        });

    public Task<Result<Guid>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "CreateRole", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var roleResult = Role.Create(request.Name, request.Description, request.Scope!.Value, request.CompanyId,
                WithIncluded(request.Permissions!));
            if (roleResult.IsFailure) return roleResult.Error;

            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(
                    ct => CreateCoreAsync(roleResult.Value, ct), CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueKeys.RoleName)
            {
                // B10: otro pedido guardó el mismo nombre después de nuestro control y antes del commit.
                return RoleErrors.AlreadyExists;
            }
        });
    }

    public Task<Result<VersionResponse>> UpdateAsync(UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<VersionResponse>(logger, timeProvider, "UpdateRole", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            Result<Role> result;
            try
            {
                result = await unitOfWork.ExecuteInTransactionAsync(
                    ct => UpdateCoreAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueKeys.RoleName)
            {
                return RoleErrors.AlreadyExists;
            }

            return VersionResponse.Of(result);
        });
    }

    public Task<Result> DeleteAsync(DeleteRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "DeleteRole", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            Result result;
            try
            {
                result = await unitOfWork.ExecuteInTransactionAsync(
                    ct => DeleteCoreAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (ForeignKeyViolationException exception)
                when (exception.ConstraintName == ForeignKeys.RoleAssignmentRole)
            {
                // Otro pedido asignó el rol después de contar sus personas y antes del commit.
                return RoleErrors.HasUsers(Math.Max(1, await roles.CountUsersAsync(request.RoleId, cancellationToken)));
            }

            return result;
        });
    }

    private async Task<Result<Guid>> CreateCoreAsync(Role role, CancellationToken cancellationToken)
    {
        var delegated = await delegation.CheckPermissionsChangeAsync([], role.Permissions, cancellationToken);
        if (delegated.IsFailure) return delegated.Error;
        if (await roles.ExistsByNameAsync(role.Name, role.CompanyId, excludedRoleId: null, cancellationToken))
            return RoleErrors.AlreadyExists;
        repository.Add(role);
        return role.Id;
    }

    private async Task<Result<Role>> UpdateCoreAsync(UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await repository.GetByIdAsync(request.RoleId, request.Version!.Value, cancellationToken);
        if (role is null) return RoleErrors.NotFound;
        if (!role.IsSystemRole && role.ChangesScope(request.Scope!.Value, request.CompanyId)
            && await roles.CountUsersAsync(role.Id, cancellationToken) is var userCount and > 0)
            return RoleErrors.ScopeChangeHasUsers(userCount);
        var permissions = WithIncluded(request.Permissions!);
        // Un rol de sistema no cambia: lo responde Update, sin pedir sus permisos.
        if (!role.IsSystemRole
            && await delegation.CheckPermissionsChangeAsync(role.Permissions, permissions, cancellationToken)
                is { IsFailure: true } denied)
            return denied.Error;
        var updated = role.Update(request.Name, request.Description, request.Scope!.Value, request.CompanyId,
            permissions);
        if (updated.IsFailure) return updated.Error;
        if (await roles.ExistsByNameAsync(role.Name, role.CompanyId, role.Id, cancellationToken))
            return RoleErrors.AlreadyExists;
        repository.MarkUpdated(role);
        return role;
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

    // Lo pedido más lo que incluye (260): «Administrar usuarios» se guarda con «Ver usuarios», así lo guardado es lo que el
    // rol permite. Un código que no es del catálogo llega al dominio, que lo rechaza.
    private static string[] WithIncluded(IEnumerable<string> permissions) =>
        Permissions.Inclusions.WithIncluded(permissions).Order(StringComparer.Ordinal).ToArray();

    private static string AreaOf(string permission) =>
        permission[..permission.IndexOf('.', StringComparison.Ordinal)];

    // grantable (269): si quien pide lo puede dar ahí, con la regla de la guarda; un rol de cualquier empresa, en la del
    // listado, o en todas si no hay.
    private static RoleResponse ToResponse(RoleRow role, PermissionsByPlace held, Guid? companyId) => new(role.Id,
        RoleTexts.Name(role.Name), RoleTexts.Description(role.Name, role.Description),
        role.Scope, role.CompanyId, role.IsSystemRole, role.UserCount, role.Permissions,
        role.Version, role.CreatedAtUtc, role.CompanyName,
        OrganizationDelegationPolicy.CanGrantListedRole(held, role.Scope, role.CompanyId, role.Permissions, companyId));
}
