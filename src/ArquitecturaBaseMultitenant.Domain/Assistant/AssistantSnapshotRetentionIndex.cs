using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Global scheduling metadata only; private snapshot content stays behind its tenant RLS.</summary>
public sealed class AssistantSnapshotRetentionIndex : Entity
{
    private AssistantSnapshotRetentionIndex() { }
    public Guid OperationId { get; private set; }
    public Guid? ScopeTenantId { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public static AssistantSnapshotRetentionIndex Create(Guid operationId, Guid? scopeTenantId, DateTime createdAtUtc)
    {
        if (operationId == Guid.Empty || scopeTenantId == Guid.Empty) throw new ArgumentException("A valid operation and scope are required.");
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(createdAtUtc));
        return new() { OperationId = operationId, ScopeTenantId = scopeTenantId, ExpiresAtUtc = createdAtUtc.AddHours(24) };
    }
}
