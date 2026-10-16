using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// «Restablecer autenticador» de otro operador (E9). Bajo el lock global de operadores y el de su cuenta verifica la
/// versión de operador, que sea un operador aceptado y distinto de quien actúa, y las reglas de
/// <see cref="PlatformOperatorGuard"/>. Revoca sesiones y autorizaciones, desafíos, clave y códigos, conserva los
/// permisos, renueva la versión y audita autor, destino y motivo en la misma UoW.
/// </summary>
internal sealed class PlatformAuthenticatorService(
    IPlatformPermissionAssignmentRepository assignments,
    PlatformOperatorGuard operators,
    IUserRepository users,
    IPlatformFactorRepository factors,
    ISignInService sessions,
    PlatformAuditWriter audit,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<PlatformAuthenticatorService> logger) : IPlatformAuthenticatorService
{
    public Task<Result<VersionResponse>> ResetAsync(Guid accountId, PlatformAccountActionRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "PlatformAuthenticatorReset", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (audit.CurrentOperatorId() == accountId) return UserErrors.CannotModifySelf;
            var saved = await unitOfWork.ExecuteInTransactionAsync(
                ct => ResetCoreAsync(accountId, request, ct), CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(saved);
        });

    private async Task<Result<PlatformPermissionAssignment>> ResetCoreAsync(Guid accountId,
        PlatformAccountActionRequest request, CancellationToken ct)
    {
        if ((await operators.CheckAnotherManagerRemainsAsync(accountId, ct)) is { IsFailure: true } last) return last.Error;
        await factors.LockAccountAsync(accountId, ct);
        var account = await users.GetByIdAsync(accountId, ct);
        if (account is null || account.Status == UserStatus.Deleted) return UserErrors.NotFound;
        var assignment = await assignments.GetByUserIdAsync(accountId, ct);
        if (assignment is null || !account.IsPlatformOperator) return PlatformOperatorErrors.NotOperator;
        // ADR 0042: una invitación sin aceptar todavía no tiene autenticador; se reenvía o se revoca.
        if (account.HasPendingOperatorInvitation) return PlatformOperatorErrors.InvitationPending;
        if (assignment.Version != request.Version!.Value) throw new ConcurrencyConflictException();

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        await factors.RemoveFactorAsync(accountId, ct);
        await factors.InvalidateChallengesAsync(accountId, nowUtc, ct);
        await factors.RevokeSessionsAsync(accountId, nowUtc, ct);
        await sessions.RevokeSessionsAsync(accountId, ct);
        assignments.MarkUpdated(assignment);
        audit.RecordAuthenticatorReset(accountId, request.Reason!);
        return assignment;
    }
}
