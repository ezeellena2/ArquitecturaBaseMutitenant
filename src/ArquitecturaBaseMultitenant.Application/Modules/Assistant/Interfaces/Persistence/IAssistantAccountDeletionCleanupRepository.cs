using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

/// <summary>Tracked deletion reads and EF cleanup in the caller's transaction and scope.</summary>
public interface IAssistantAccountDeletionCleanupRepository
{
    void RequireTransaction();
    Task<IReadOnlyList<AssistantTurnWork>> ListActiveWorksAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<AssistantTurnWork>> LockWorksAsync(Guid userId, Guid? tenantId, CancellationToken ct);
    Task LockFinanceAsync(Guid userId, Guid? tenantId, CancellationToken ct);
    Task<IReadOnlyList<AssistantFinancialOperation>> ListFinancialOperationsAsync(Guid userId, Guid? tenantId, CancellationToken ct);
    void RemoveWorks(IEnumerable<AssistantTurnWork> works);
    Task RemovePrivateContentAsync(Guid userId, Guid tenantId, DateTime nowUtc, CancellationToken ct);
    Task RemoveGlobalContentAsync(Guid userId, DateTime nowUtc, CancellationToken ct);
}
