using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Auditing;

/// <summary>Registra actor y cambio JSON de una entidad dentro de un tenant; el interceptor o AuditLog la agrega al guardado.</summary>
public sealed class AuditEntry : Entity, ITenantOwned
{
    private AuditEntry()
    {
        EntityType = string.Empty;
        Changes = string.Empty;
    }

    public Guid TenantId { get; private set; }

    public AuditActorKind ActorKind { get; private set; }

    public Guid? ActorId { get; private set; }

    public AuditAction Action { get; private set; }

    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public string Changes { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public static AuditEntry Create(Guid tenantId, AuditActorKind actorKind, Guid? actorId,
        AuditAction action, string entityType, Guid entityId, string changes, DateTime occurredAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("The tenant id cannot be empty.", nameof(tenantId));
        }

        if (!Enum.IsDefined(actorKind) || actorKind != AuditActorKind.System && (actorId is null || actorId == Guid.Empty))
        {
            throw new ArgumentException("The actor is invalid.", nameof(actorId));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("The entity id cannot be empty.", nameof(entityId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(changes);
        using var document = JsonDocument.Parse(changes);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The change data must be a JSON object.", nameof(changes));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The occurrence instant must be UTC.", nameof(occurredAtUtc));
        }

        return new AuditEntry
        {
            TenantId = tenantId,
            ActorKind = actorKind,
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Changes = changes,
            OccurredAtUtc = occurredAtUtc,
        };
    }
}
