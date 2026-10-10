using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSettingsAuditWriter(ICurrentUser user, ISecurityEventRepository events, TimeProvider clock, IRequestInfo request)
{
    internal void RecordGlobal(string reason, SecurityEventChanges changes)
    {
        events.Add(SecurityEvent.ForPlatformSettings(AuditActorKind.PlatformOperator, OperatorId(), reason,
            clock.GetUtcNow().UtcDateTime).WithEvidence(changes, request.IpAddress));
    }
    internal void RecordPolicy(Guid tenantId, string reason, SecurityEventChanges changes)
    {
        events.Add(SecurityEvent.ForTenantFeatures(AuditActorKind.PlatformOperator, OperatorId(), tenantId, reason,
            clock.GetUtcNow().UtcDateTime).WithEvidence(changes, request.IpAddress));
    }
    private Guid OperatorId() => user.Access == Access.Platform && user.UserId is { } id ? id :
        throw new InvalidOperationException("An authenticated platform operator is required.");
}
