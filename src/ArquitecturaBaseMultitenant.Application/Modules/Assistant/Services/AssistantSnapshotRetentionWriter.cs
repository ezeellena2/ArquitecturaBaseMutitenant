using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Snapshot policy runs inside the caller's transaction after technical locks.</summary>
internal sealed class AssistantSnapshotRetentionWriter(IAssistantSnapshotRetentionRepository repository)
{
    internal async Task<int> PurgeExpiredAsync(AssistantSnapshotRetentionCandidate candidate, DateTime nowUtc, CancellationToken ct)
    {
        var row = await repository.LockExpiredAsync(candidate, nowUtc, ct);
        if (!row.IsAvailable || row.Operation is { } operation &&
            operation.CreatedAtUtc > nowUtc.Subtract(AssistantSnapshotRetentionIndex.ContentLifetime)) return 0;
        row.Operation?.RemoveContent();
        await repository.RemoveIndicesAsync([candidate.OperationId], ct);
        return 1;
    }

    internal async Task PurgeAccountAsync(Guid userId, CancellationToken ct)
    {
        var rows = await repository.LockAccountOperationsAsync(userId, ct);
        foreach (var row in rows)
        {
            if (row.OwnerUserId == userId) repository.RemoveOperation(row);
            else row.RemoveContent();
        }
        await repository.RemoveIndicesAsync(rows.Select(row => row.Id).ToArray(), ct);
    }
}
