using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Prepara el evento del operador en la transacción que modifica la configuración.</summary>
internal sealed class PlatformSettingsAuditWriter(ICurrentUser currentUser, ISecurityEventRepository events,
    TimeProvider timeProvider, IRequestInfo requestInfo)
{
    internal void Record(string reason, SecurityEventChanges changes)
    {
        if (currentUser.Access != Access.Platform || currentUser.UserId is not { } operatorId)
            throw new InvalidOperationException("Platform settings require an authenticated operator.");
        events.Add(SecurityEvent.ForPlatformSettings(AuditActorKind.PlatformOperator, operatorId, reason,
            timeProvider.GetUtcNow().UtcDateTime).WithEvidence(changes, requestInfo.IpAddress));
    }
}
