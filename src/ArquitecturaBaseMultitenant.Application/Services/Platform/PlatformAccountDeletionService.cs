using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Baja iniciada por Plataforma (ADR 0035): sin reautenticar a la persona y con la misma gracia. Una cuenta
/// activa pasa a PendingDeletion y puede cancelarla ingresando; una suspendida sigue suspendida con la fecha.
/// Bloquean el único Dueño de una organización no cerrada (lo controla el participante de membresías, con la misma
/// regla que la baja que pide la persona) y el último operador que administra operadores.
/// Plataforma no cancela ni acorta la gracia. La acción exige motivo y no lleva versión: la fila de la cuenta cambia
/// con cualquier escritura y daría un 409 falso. La protege el estado, bajo el lock de la cuenta: una baja ya
/// programada recibe <c>AlreadyPending</c> y una cuenta que no está activa ni suspendida, el error de estado.
/// </summary>
internal sealed class PlatformAccountDeletionService(IUserRepository users, ILoginMethodRepository accounts,
    PlatformOperatorGuard operators, AccountDeletionScheduler scheduler,
    PlatformAuditWriter audit, IAccessStatusCache statuses, ICurrentUser currentUser, IUnitOfWork unitOfWork,
    IRequestValidator validator, TimeProvider clock, ILogger<PlatformAccountDeletionService> logger)
    : IPlatformAccountDeletionService
{
    public Task<Result> RequestDeletionAsync(Guid accountId, PlatformAccountReasonRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RequestPlatformAccountDeletion", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (currentUser.UserId is not { } operatorId) return UserErrors.NotFound;
            if (operatorId == accountId) return UserErrors.CannotModifySelf;
            var result = await unitOfWork.ExecuteInTransactionAsync(tx => RequestCoreAsync(accountId, operatorId, request, tx),
                CommitPolicy.OnSuccess, ct);
            if (result.IsSuccess) await statuses.InvalidateUserAsync(accountId, ct);
            return result;
        });

    private async Task<Result> RequestCoreAsync(Guid accountId, Guid operatorId, PlatformAccountReasonRequest request,
        CancellationToken ct)
    {
        if (await operators.CheckAnotherManagerRemainsAsync(accountId, ct) is { IsFailure: true } kept) return kept;
        await accounts.LockUserAsync(accountId, ct);
        var account = await users.GetByIdAsync(accountId, ct);
        if (account is null) return UserErrors.NotFound;
        // ADR 0042: una invitación de operador sin aceptar solo se reenvía o se revoca.
        if (account.HasPendingOperatorInvitation) return PlatformOperatorErrors.InvitationPending;
        if (account.DeletionScheduledForUtc is not null || account.Status == UserStatus.PendingDeletion)
            return AccountDeletionErrors.AlreadyPending;
        if (account.Status is not (UserStatus.Active or UserStatus.Suspended)) return UserErrors.InvalidTransition;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (await scheduler.CheckBlocksAsync(accountId, nowUtc, ct) is { } blocked) return blocked;

        var scheduled = await scheduler.ScheduleAsync(account, request.Reason!, nowUtc, operatorId, ct);
        if (scheduled.IsFailure) return scheduled.Error;
        var changes = new SecurityEventChanges();
        changes.AddEnum("status", account.Status,
            account.Status == UserStatus.Active ? UserStatus.PendingDeletion : account.Status);
        changes.AddUtc("deletion.scheduledForUtc", null, scheduled.Value);
        audit.RecordAccount(accountId, SecurityEventType.AccountDeletionRequested, request.Reason!, changes);
        return Result.Success();
    }
}
