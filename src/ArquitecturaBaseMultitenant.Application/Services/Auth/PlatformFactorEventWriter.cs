using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Agrega los eventos automáticos del segundo factor del propio operador (E9) en la transacción del llamador: configuró
/// su autenticador, usó un código de recuperación o confirmó su identidad. Llevan un motivo de sistema traducido y la
/// IP enmascarada, sin códigos ni secretos.
/// </summary>
internal sealed class PlatformFactorEventWriter(ISecurityEventRepository events, IRequestInfo requestInfo, TimeProvider clock)
{
    internal void RecordConfigured(Guid operatorId) =>
        Record(operatorId, SecurityEventType.PlatformAuthenticatorConfigured, AuditTexts.PlatformAuthenticatorConfiguredReason());

    internal void RecordRecoveryCodeUsed(Guid operatorId) =>
        Record(operatorId, SecurityEventType.PlatformRecoveryCodeUsed, AuditTexts.PlatformRecoveryCodeUsedReason());

    internal void RecordReauthenticated(Guid operatorId) =>
        Record(operatorId, SecurityEventType.PlatformReauthenticated, AuditTexts.PlatformReauthenticatedReason());

    private void Record(Guid operatorId, SecurityEventType type, string reason) =>
        events.Add(SecurityEvent.ForPlatformSecondFactor(type, operatorId, operatorId, reason, clock.GetUtcNow().UtcDateTime)
            .WithRequestIp(requestInfo.IpAddress));
}
