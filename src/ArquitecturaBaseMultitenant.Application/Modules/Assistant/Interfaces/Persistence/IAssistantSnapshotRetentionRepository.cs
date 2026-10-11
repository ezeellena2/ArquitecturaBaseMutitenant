using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantSnapshotRetentionRepository
{
    Task<IReadOnlyList<AssistantSnapshotRetentionCandidate>> ListExpiredAsync(DateTime nowUtc, CancellationToken ct,
        AssistantSnapshotRetentionCandidate? after = null);
    Task<int> PurgeExpiredAsync(AssistantSnapshotRetentionCandidate candidate, DateTime nowUtc, CancellationToken ct);
    Task PurgeAccountAsync(Guid userId, CancellationToken ct);
}
