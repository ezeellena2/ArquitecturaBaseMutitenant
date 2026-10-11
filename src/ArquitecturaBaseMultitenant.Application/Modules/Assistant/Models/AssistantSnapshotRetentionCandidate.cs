namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantSnapshotRetentionCandidate(Guid OperationId, Guid? ScopeTenantId)
{
    public Guid IndexId { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
}
