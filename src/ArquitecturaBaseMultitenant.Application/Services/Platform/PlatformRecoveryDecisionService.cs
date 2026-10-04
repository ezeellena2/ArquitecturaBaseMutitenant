using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Resuelve pedidos de «Recuperar mi cuenta». Aprobar suma el correo nuevo, verificado, como método principal de la
/// cuenta, sin emitir una sesión; rechazar conserva los métodos. Cada decisión lleva motivo, versión y SecurityEvent.
/// </summary>
internal sealed class PlatformRecoveryDecisionService(
    IAccountRecoveryRepository recoveries,
    ILoginMethodRepository methods,
    IUserRepository users,
    IUserLookup lookup,
    LoginCodeIssuer codes,
    LoginMethodDomainAssigner emailDomains,
    PlatformAudit audit,
    PlatformRecoveryNotifier notifier,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<PlatformRecoveryDecisionService> logger) : IPlatformRecoveryDecisionService
{
    public Task<Result> ApproveAsync(Guid id, PlatformAccountActionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ApproveAccountRecovery", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var operatorId = audit.CurrentOperatorId();
            return await unitOfWork.ExecuteInTransactionAsync<Result>(async work =>
            {
                var recovery = await recoveries.GetForUpdateAsync(id, work);
                if (recovery is null) return AccountRecoveryErrors.NotFound;
                if (recovery.Version != request.Version!.Value) throw new ConcurrencyConflictException();
                if (recovery.Status != AccountRecoveryStatus.Pending) return AccountRecoveryErrors.AlreadyResolved;
                if (recovery.UserId is not { } userId) return AccountRecoveryErrors.NoAccount;
                await methods.LockUserAsync(userId, work);
                var account = await users.GetByIdAsync(userId, work);
                if (account is null || account.Status == UserStatus.Deleted) return AccountRecoveryErrors.NoAccount;
                if (account.IsPlatformOperator) return AccountRecoveryErrors.OperatorAccount;
                var email = Email.Create(recovery.NewMethodValue).Value;
                await codes.LockDestinationAsync(LoginCodeDestination.ForEmail(email), work);
                var owners = await lookup.FindVerifiedUsersByEmailAsync(email, work);
                if (owners.Contains(userId)) return AccountRecoveryErrors.MethodAlreadyOnAccount;
                if (owners.Count > 0) return AccountRecoveryErrors.MethodInUse;
                // Un correo agregado y sin verificar por otra cuenta se retira, como al verificarlo desde Mi cuenta.
                if (await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, work) is { } existing
                    && await methods.GetByIdAsync(existing.MethodId, work) is { VerifiedAtUtc: null } unverified)
                    methods.Remove(unverified);

                var nowUtc = clock.GetUtcNow().UtcDateTime;
                var approved = recovery.Approve(operatorId, request.Reason!, nowUtc);
                if (approved.IsFailure) return approved;
                var method = LoginMethod.CreateEmail(userId, email);
                method.Verify(nowUtc);
                await emailDomains.AssignIfVerifiedAsync(method, email, work);
                await methods.ClearPrimaryAsync(userId, work);
                method.MakePrimary();
                methods.Add(method);
                await users.SetPrimaryEmailAsync(userId, email, work);
                audit.RecordRecovery(userId, SecurityEventType.AccountRecoveryApproved, request.Reason!,
                    StatusChange(AccountRecoveryStatus.Approved));
                await notifier.ApprovedAsync(account, method,
                    [.. await methods.ListByUserIdAsync(userId, work), method], work);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        });

    public Task<Result> RejectAsync(Guid id, PlatformAccountActionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RejectAccountRecovery", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var operatorId = audit.CurrentOperatorId();
            return await unitOfWork.ExecuteInTransactionAsync<Result>(async work =>
            {
                var recovery = await recoveries.GetForUpdateAsync(id, work);
                if (recovery is null) return AccountRecoveryErrors.NotFound;
                if (recovery.Version != request.Version!.Value) throw new ConcurrencyConflictException();
                var rejected = recovery.Reject(operatorId, request.Reason!, clock.GetUtcNow().UtcDateTime);
                if (rejected.IsFailure) return rejected;
                audit.RecordRecovery(recovery.UserId, SecurityEventType.AccountRecoveryRejected, request.Reason!,
                    StatusChange(AccountRecoveryStatus.Rejected));
                await notifier.RejectedAsync(recovery.NewMethodValue, work);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        });

    private static SecurityEventChanges StatusChange(AccountRecoveryStatus after)
    {
        var changes = new SecurityEventChanges();
        changes.AddEnum("recovery.status", AccountRecoveryStatus.Pending, after);
        return changes;
    }
}
