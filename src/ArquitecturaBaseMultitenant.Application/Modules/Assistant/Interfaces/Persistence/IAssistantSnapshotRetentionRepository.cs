using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantSnapshotRetentionRepository
{
    Task<IReadOnlyList<AssistantSnapshotRetentionCandidate>> ListExpiredAsync(DateTime nowUtc, CancellationToken ct,
        AssistantSnapshotRetentionCandidate? after = null);
    Task<AssistantSnapshotCleanupRow> LockExpiredAsync(AssistantSnapshotRetentionCandidate candidate, DateTime nowUtc, CancellationToken ct);
    Task RemoveIndicesAsync(IReadOnlyList<Guid> operationIds, CancellationToken ct);
    Task<IReadOnlyList<AssistantDurableOperationBase>> LockAccountOperationsAsync(Guid userId, CancellationToken ct);
    void RemoveOperation(AssistantDurableOperationBase operation);
}
