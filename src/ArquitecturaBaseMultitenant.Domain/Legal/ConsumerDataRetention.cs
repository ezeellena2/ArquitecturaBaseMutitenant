using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Legal;

/// <summary>Metadatos técnicos de una copia retenida: nunca contiene el nombre ni los contactos de la persona.</summary>
public sealed class ConsumerDataRetention : Entity
{
    private ConsumerDataRetention() { }

    public string EntityTypeKey { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public Guid ConsumerTenantId { get; private set; }
    public Guid BusinessTenantId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime RetainUntilUtc { get; private set; }
    public DateTime? CopyReplacedAtUtc { get; private set; }

    public static ConsumerDataRetention Schedule(string entityTypeKey, Guid entityId,
        Guid consumerTenantId, Guid businessTenantId, string reason, DateTime retainUntilUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityTypeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (entityId == Guid.Empty || consumerTenantId == Guid.Empty || businessTenantId == Guid.Empty)
            throw new ArgumentException("Retention needs the shared row and both parties.");
        RequireUtc(retainUntilUtc);
        return new ConsumerDataRetention
        {
            EntityTypeKey = entityTypeKey, EntityId = entityId,
            ConsumerTenantId = consumerTenantId, BusinessTenantId = businessTenantId,
            Reason = reason, RetainUntilUtc = retainUntilUtc,
        };
    }

    public void MarkCopyReplaced(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (nowUtc < RetainUntilUtc) throw new InvalidOperationException("The retention period has not ended.");
        CopyReplacedAtUtc ??= nowUtc;
    }

    private static void RequireUtc(DateTime instantUtc)
    {
        if (instantUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The instant must be UTC.", nameof(instantUtc));
    }
}
