using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantRetentionRepository
{
    Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken ct);
    Task<int> RetentionDaysAsync(CancellationToken ct);
    Task<IReadOnlyList<AssistantConversationRetentionRow>> ListExpiredAsync(DateTime cutoffUtc, Guid? after, CancellationToken ct);
    Task<IReadOnlyList<AssistantFileCleanupRow>> ListCleanupAsync(CancellationToken ct);
    Task RemoveCleanupAsync(Guid id, CancellationToken ct);
    Task<bool> IsStorageReferencedAsync(string storageKey, CancellationToken ct);
}
