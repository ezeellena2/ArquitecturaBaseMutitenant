using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformTenantAudit(ICurrentUser currentUser, ISecurityEventRepository events,
    TimeProvider clock, IRequestInfo requestInfo)
{
    internal void RecordFeatures(Guid tenantId, string reason, SecurityEventChanges changes)
    {
        if (currentUser.Access != Access.Platform || currentUser.UserId is not { } id)
            throw new InvalidOperationException("An authenticated platform operator is required.");
        events.Add(SecurityEvent.ForTenantFeatures(AuditActorKind.PlatformOperator, id, tenantId, reason,
            clock.GetUtcNow().UtcDateTime).WithEvidence(changes, requestInfo.IpAddress));
    }

    internal void Record(Guid tenantId, SecurityEventType type, string reason, SecurityEventChanges changes)
    {
        if (currentUser.Access != Access.Platform || currentUser.UserId is not { } id)
            throw new InvalidOperationException("An authenticated platform operator is required.");
        events.Add(SecurityEvent.ForTenantAdministration(type, id, tenantId, reason,
            clock.GetUtcNow().UtcDateTime).WithEvidence(changes, requestInfo.IpAddress));
    }
}
