using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Agrega el SecurityEvent del operador con su destino, el diff técnico y la IP enmascarada.</summary>
internal sealed class PlatformAudit(ICurrentUser currentUser, ISecurityEventRepository events,
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

    /// <summary>El operador autenticado que actúa; lo guarda la decisión de un pedido de recuperación.</summary>
    internal Guid CurrentOperatorId() =>
        currentUser.Access == Access.Platform && currentUser.UserId is { } id
            ? id
            : throw new InvalidOperationException("An authenticated platform operator is required.");

    internal void RecordRecovery(Guid? accountId, SecurityEventType type, string reason, SecurityEventChanges changes) =>
        events.Add(SecurityEvent.ForAccountRecovery(type, CurrentOperatorId(), accountId, reason,
            clock.GetUtcNow().UtcDateTime).WithEvidence(changes, requestInfo.IpAddress));

    internal void RecordAccount(Guid accountId, SecurityEventType type, string reason, SecurityEventChanges changes)
    {
        if (currentUser.Access != Access.Platform || currentUser.UserId is not { } id)
            throw new InvalidOperationException("An authenticated platform operator is required.");
        events.Add(SecurityEvent.ForAccountAdministration(type, id, accountId, reason,
            clock.GetUtcNow().UtcDateTime).WithEvidence(changes, requestInfo.IpAddress));
    }
}
