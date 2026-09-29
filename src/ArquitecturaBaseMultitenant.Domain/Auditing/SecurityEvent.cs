using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Auditing;

/// <summary>Evento de seguridad global y append-only, sin direcciones ni secretos.</summary>
public sealed class SecurityEvent : Entity
{
    private SecurityEvent() { }

    public SecurityEventType Type { get; private set; }
    public AuditActorKind ActorKind { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid? TargetTenantId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static SecurityEvent ForAccount(SecurityEventType type, Guid actorId, DateTime occurredAtUtc)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("The actor id cannot be empty.", nameof(actorId));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The occurrence instant must be UTC.", nameof(occurredAtUtc));
        }

        return new SecurityEvent
        {
            Type = type,
            ActorKind = AuditActorKind.User,
            ActorId = actorId,
            OccurredAtUtc = occurredAtUtc,
        };
    }
}
