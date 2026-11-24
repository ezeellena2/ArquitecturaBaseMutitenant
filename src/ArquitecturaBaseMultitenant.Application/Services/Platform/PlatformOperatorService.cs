using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Cambia o quita los permisos de un operador con la versión de su asignación y motivo, y guarda lo pedido más lo que
/// incluye (decisión 260). Nadie actúa sobre sí mismo y
/// siempre queda un operador activo con <c>platform.operators.manage</c> (lock global de <see cref="PlatformOperatorGuard"/>).
/// Quitar el acceso cierra todas sus sesiones; la cuenta queda sin operador ni permisos. Una invitación sin aceptar
/// no se cambia ni se quita: se reenvía o se revoca (ADR 0042).
/// </summary>
internal sealed class PlatformOperatorService(
    IPlatformPermissionAssignmentRepository assignments,
    PlatformOperatorGuard operators,
    IUserRepository users,
    ISignInService sessions,
    PlatformAuditWriter audit,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<PlatformOperatorService> logger) : IPlatformOperatorService
{
    public Task<Result<VersionResponse>> ChangePermissionsAsync(Guid accountId, ChangePlatformOperatorRequest request,
        CancellationToken ct) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "ChangePlatformOperatorPermissions", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var permissions = PlatformPermissionAssignment.Validate(request.Permissions);
            if (permissions.IsFailure) return permissions.Error;
            // Lo pedido más lo que incluye (260): «Administrar cuentas» se guarda con «Ver cuentas».
            var granted = PlatformPermissions.Inclusions.WithIncluded(request.Permissions!);
            if (audit.CurrentOperatorId() == accountId) return UserErrors.CannotModifySelf;
            var saved = await unitOfWork.ExecuteInTransactionAsync<Result<PlatformPermissionAssignment>>(async work =>
            {
                var assignment = await LoadAsync(accountId, request.Version!.Value, work);
                if (assignment.IsFailure) return assignment.Error;
                if (!granted.Contains(PlatformPermissions.Operators.Manage, StringComparer.Ordinal)
                    && (await operators.CheckAnotherManagerRemainsAsync(accountId, work)) is { IsFailure: true } last)
                    return last.Error;
                var before = assignment.Value.Permissions.ToArray();
                var changed = assignment.Value.ChangePermissions(granted);
                if (changed.IsFailure) return changed.Error;
                audit.RecordOperator(accountId, SecurityEventType.PlatformOperatorPermissionsChanged, request.Reason!,
                    PlatformOperatorChanges.Between(before, granted));
                return assignment.Value;
            }, CommitPolicy.OnSuccess, ct);
            return VersionResponse.Of(saved);
        });

    public Task<Result> RemoveAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RemovePlatformOperator", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (audit.CurrentOperatorId() == accountId) return UserErrors.CannotModifySelf;
            return await unitOfWork.ExecuteInTransactionAsync<Result>(async work =>
            {
                var assignment = await LoadAsync(accountId, request.Version!.Value, work);
                if (assignment.IsFailure) return assignment.Error;
                if ((await operators.CheckAnotherManagerRemainsAsync(accountId, work)) is { IsFailure: true } last)
                    return last;
                var before = assignment.Value.Permissions.ToArray();
                assignments.Remove(assignment.Value);
                await users.SetPlatformOperatorAsync(accountId, false, work);
                await sessions.RevokeSessionsAsync(accountId, work);
                audit.RecordOperator(accountId, SecurityEventType.PlatformOperatorRemoved, request.Reason!,
                    PlatformOperatorChanges.Between(before, []));
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        });

    private async Task<Result<PlatformPermissionAssignment>> LoadAsync(Guid accountId, uint version, CancellationToken ct)
    {
        var account = await users.GetByIdAsync(accountId, ct);
        if (account is null || account.Status == UserStatus.Deleted) return UserErrors.NotFound;
        var assignment = await assignments.GetByUserIdAsync(accountId, ct);
        if (assignment is null || !account.IsPlatformOperator) return PlatformOperatorErrors.NotOperator;
        // ADR 0042: una invitación sin aceptar solo se reenvía o se revoca.
        if (account.HasPendingOperatorInvitation) return PlatformOperatorErrors.InvitationPending;
        if (assignment.Version != version) throw new ConcurrencyConflictException();
        return assignment;
    }

    public Task<Result<IReadOnlyList<PlatformPermissionItem>>> GetPermissionCatalogAsync(CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "GetPlatformPermissionCatalog", () =>
        {
            // Lo que incluye cada permiso (260); grantable sigue en true hasta la B1.
            IReadOnlyList<PlatformPermissionItem> catalog =
                [.. PlatformPermissions.All.Select(code => new PlatformPermissionItem(code, PlatformPermissions.Inclusions.Of(code), true))];
            return Task.FromResult(Result.Success(catalog));
        });
}
