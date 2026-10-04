using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Invita operadores (ADR 0042). Un correo que ya tiene cuenta se rechaza con su motivo: es la excepción de la ADR
/// 0041, porque invitar pide <c>platform.operators.manage</c>. La invitación figura en Cuentas como pendiente hasta el
/// primer ingreso con código a Plataforma y vence a los 7 días. Reenviar renueva el plazo y manda el correo de nuevo;
/// revocar la borra y libera el correo. Cada acción deja su SecurityEvent con un motivo fijo.
/// </summary>
internal sealed class PlatformOperatorInvitationService(
    PlatformOperatorProvisioner provisioner,
    PlatformOperatorInvitationNotifier notifier,
    IUserLookup lookup,
    LoginCodeIssuer codes,
    PlatformAudit audit,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<PlatformOperatorInvitationService> logger) : IPlatformOperatorInvitationService
{
    public Task<Result<PlatformOperatorInvitationResponse>> InviteAsync(InvitePlatformOperatorRequest request,
        CancellationToken ct) =>
        OperationLog.RunAsync<PlatformOperatorInvitationResponse>(logger, clock, "InvitePlatformOperator", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var permissions = PlatformPermissionAssignment.Validate(request.Permissions);
            if (permissions.IsFailure) return permissions.Error;
            var email = request.Email!;
            return await unitOfWork.ExecuteInTransactionAsync<Result<PlatformOperatorInvitationResponse>>(async work =>
            {
                // El mismo lock que el ingreso y el registro con ese correo: nadie crea la cuenta en el medio.
                await codes.LockDestinationAsync(LoginCodeDestination.ForEmail(email), work);
                if (await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, work) is not null
                    || (await lookup.FindVerifiedUsersByEmailAsync(email, work)).Count > 0)
                    return PlatformOperatorErrors.AccountExists;
                var invited = await provisioner.CreateAsync(email, request.Permissions!, clock.GetUtcNow().UtcDateTime, work);
                if (invited.IsFailure) return invited.Error;
                audit.RecordOperator(invited.Value.Id, SecurityEventType.PlatformOperatorInvited,
                    AuditTexts.OperatorInvitationReason(), PlatformOperatorChanges.Between([], request.Permissions!));
                await notifier.EnqueueAsync(invited.Value, audit.CurrentOperatorId(), work);
                return new PlatformOperatorInvitationResponse(invited.Value.Id, invited.Value.ExpiresAtUtc);
            }, CommitPolicy.OnSuccess, ct);
        });

    public Task<Result<PlatformOperatorInvitationResponse>> ResendAsync(Guid accountId, CancellationToken ct) =>
        OperationLog.RunAsync<PlatformOperatorInvitationResponse>(logger, clock, "ResendPlatformOperatorInvitation", () =>
            unitOfWork.ExecuteInTransactionAsync<Result<PlatformOperatorInvitationResponse>>(async work =>
            {
                var renewed = await provisioner.RenewAsync(accountId, clock.GetUtcNow().UtcDateTime, work);
                if (renewed.IsFailure) return renewed.Error;
                var changes = new SecurityEventChanges();
                changes.AddUtc("invitation.expiresAtUtc", renewed.Value.PreviousExpiresAtUtc, renewed.Value.ExpiresAtUtc);
                audit.RecordOperator(accountId, SecurityEventType.PlatformOperatorInvitationResent,
                    AuditTexts.OperatorInvitationResendReason(), changes);
                await notifier.EnqueueAsync(renewed.Value, audit.CurrentOperatorId(), work);
                return new PlatformOperatorInvitationResponse(accountId, renewed.Value.ExpiresAtUtc);
            }, CommitPolicy.OnSuccess, ct));

    public Task<Result> RevokeAsync(Guid accountId, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RevokePlatformOperatorInvitation", () =>
            unitOfWork.ExecuteInTransactionAsync<Result>(async work =>
            {
                var discarded = await provisioner.DiscardAsync(accountId, clock.GetUtcNow().UtcDateTime, work);
                if (discarded.IsFailure) return discarded.Error;
                audit.RecordOperator(accountId, SecurityEventType.PlatformOperatorInvitationRevoked,
                    AuditTexts.OperatorInvitationRevokeReason(), PlatformOperatorChanges.Between(discarded.Value, []));
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct));
}

/// <summary>El diff de permisos de un operador: cada clave técnica que cambió, con antes y después.</summary>
internal static class PlatformOperatorChanges
{
    public static SecurityEventChanges Between(IReadOnlyCollection<string> before, IReadOnlyCollection<string> after)
    {
        var changes = new SecurityEventChanges();
        foreach (var permission in Domain.Authorization.PlatformPermissions.All)
            changes.AddBoolean(permission, before.Contains(permission, StringComparer.Ordinal),
                after.Contains(permission, StringComparer.Ordinal));
        return changes;
    }
}
