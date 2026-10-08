using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

using ArquitecturaBaseMultitenant.Application.Services.Identity;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Resuelve pedidos de «Recuperar mi cuenta». Aprobar suma el destino nuevo, verificado, como método principal de la
/// cuenta, sin emitir una sesión; rechazar conserva los métodos. Cada decisión lleva motivo, versión y SecurityEvent.
/// </summary>
internal sealed class PlatformRecoveryDecisionService(
    IAccountRecoveryRepository recoveries,
    ILoginMethodRepository methods,
    IUserRepository users,
    AccountRecoveryGuard guard,
    LoginDestinationResolver destinations,
    LoginMethodLinker linker,
    PlatformAuditWriter audit,
    PlatformRecoveryNotifier notifier,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<PlatformRecoveryDecisionService> logger) : IPlatformRecoveryDecisionService
{
    public Task<Result<VersionResponse>> ApproveAsync(Guid id, PlatformAccountActionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "ApproveAccountRecovery", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var operatorId = audit.CurrentOperatorId();
            var saved = await unitOfWork.ExecuteInTransactionAsync<Result<AccountRecoveryRequest>>(async work =>
            {
                var hint = await recoveries.GetIdentityHintAsync(id, work);
                if (hint is null) return AccountRecoveryErrors.NotFound;
                var resolved = destinations.FromContact(hint.NewMethodType, hint.NewMethodValue);
                if (resolved.IsFailure) return resolved.Error;
                var destination = resolved.Value;
                await guard.LockAsync(destination, hint.UserId, work);
                var recovery = await recoveries.GetForUpdateAsync(id, work);
                if (recovery is null) return AccountRecoveryErrors.NotFound;
                if (recovery.Version != request.Version!.Value) throw new ConcurrencyConflictException();
                if (recovery.Status != AccountRecoveryStatus.Pending) return AccountRecoveryErrors.AlreadyResolved;
                if (recovery.UserId is not { } userId) return AccountRecoveryErrors.NoAccount;
                var account = await users.GetByIdAsync(userId, work);
                if (account is null || account.Status == UserStatus.Deleted) return AccountRecoveryErrors.NoAccount;
                if (account.IsPlatformOperator) return AccountRecoveryErrors.OperatorAccount;
                var valid = await guard.CheckNewAsync(destination, work, userId);
                if (valid.IsFailure) return valid.Error;
                // Un destino pendiente no reserva la propiedad frente a quien ya probó posesión.
                if (await guard.FindPendingAsync(destination, work) is { } existing
                    && await methods.GetByIdAsync(existing.MethodId, work) is { VerifiedAtUtc: null } unverified)
                    methods.Remove(unverified);

                var nowUtc = clock.GetUtcNow().UtcDateTime;
                var approved = recovery.Approve(operatorId, request.Reason!, nowUtc);
                if (approved.IsFailure) return approved.Error;
                var method = LoginDestinationResolver.Create(userId, destination);
                var linked = await linker.VerifyAsync(method, nowUtc, work, replacePrimary: true);
                if (linked.IsFailure) return linked.Error;
                methods.Add(method);
                audit.RecordRecovery(userId, SecurityEventType.AccountRecoveryApproved, request.Reason!,
                    StatusChange(AccountRecoveryStatus.Approved));
                await notifier.ApprovedAsync(account, method,
                    [.. await methods.ListByUserIdAsync(userId, work), method], work);
                return recovery;
            }, CommitPolicy.OnSuccess, ct);
            return VersionResponse.Of(saved);
        });

    public Task<Result<VersionResponse>> RejectAsync(Guid id, PlatformAccountActionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "RejectAccountRecovery", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var operatorId = audit.CurrentOperatorId();
            var saved = await unitOfWork.ExecuteInTransactionAsync<Result<AccountRecoveryRequest>>(async work =>
            {
                var recovery = await recoveries.GetForUpdateAsync(id, work);
                if (recovery is null) return AccountRecoveryErrors.NotFound;
                if (recovery.Version != request.Version!.Value) throw new ConcurrencyConflictException();
                var rejected = recovery.Reject(operatorId, request.Reason!, clock.GetUtcNow().UtcDateTime);
                if (rejected.IsFailure) return rejected.Error;
                audit.RecordRecovery(recovery.UserId, SecurityEventType.AccountRecoveryRejected, request.Reason!,
                    StatusChange(AccountRecoveryStatus.Rejected));
                await notifier.RejectedAsync(recovery.NewMethodType, recovery.NewMethodValue, work);
                return recovery;
            }, CommitPolicy.OnSuccess, ct);
            return VersionResponse.Of(saved);
        });

    private static SecurityEventChanges StatusChange(AccountRecoveryStatus after)
    {
        var changes = new SecurityEventChanges();
        changes.AddEnum("recovery.status", AccountRecoveryStatus.Pending, after);
        return changes;
    }
}
