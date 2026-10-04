using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
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
/// 0041, porque invitar pide <c>platform.operators.manage</c>. El invitado aparece enseguida en Cuentas y entra con
/// código a Plataforma; el aviso de invitación lleva un texto provisorio hasta el tablero de Mensajes.
/// </summary>
internal sealed class PlatformOperatorInvitationService(
    PlatformOperatorProvisioner provisioner,
    IUserLookup lookup,
    LoginCodeIssuer codes,
    PlatformAudit audit,
    IEnumerable<IAccountNoticeChannel> channels,
    UserCultures cultures,
    IPublicOrigin origin,
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
                var created = await provisioner.CreateAsync(email, request.Permissions!, clock.GetUtcNow().UtcDateTime, work);
                if (created.IsFailure) return created.Error;
                audit.RecordOperator(created.Value, SecurityEventType.PlatformOperatorInvited,
                    AuditTexts.OperatorInvitationReason(), PlatformOperatorChanges.Between([], request.Permissions!));
                var url = origin.Value is { } value ? new Uri(value, "/plataforma").AbsoluteUri : "/plataforma";
                var channel = channels.FirstOrDefault(item => item.MethodType == LoginMethodType.Email)
                    ?? throw new InvalidOperationException("The email notice channel is not registered.");
                await channel.EnqueueAsync(created.Value, email.Value, new AccountNotice.OperatorInvited(url),
                    await cultures.ResolveAsync(null, null, work), work);
                return new PlatformOperatorInvitationResponse(created.Value);
            }, CommitPolicy.OnSuccess, ct);
        });
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
