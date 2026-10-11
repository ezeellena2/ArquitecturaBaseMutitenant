using ArquitecturaBaseMultitenant.Application.Models.Idempotency;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantDurableOperationRow(IdempotentReference Reference, Guid ConversationId,
    string BodyHash, string Route, string? SnapshotJson, string? QuickKey, uint Version, DateTime CreatedAtUtc = default)
{
    public IReadOnlyList<Guid> SubjectUserIds { get; init; } = [];
}
