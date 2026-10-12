using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

/// <summary>Todas las escrituras requieren la UOW del caller y su binding original; nunca consultan chats.</summary>
public interface IAssistantFinanceRepository
{
    Task LockAsync(AssistantFinancialBinding binding, Guid operationId, CancellationToken ct);
    Task<AssistantFinancialOperation?> GetOperationAsync(AssistantFinancialBinding binding, Guid operationId, CancellationToken ct);
    Task<AssistantPlatformKeyDailyQuota?> GetPlatformQuotaAsync(Guid userId, DateOnly dayUtc, CancellationToken ct);
    Task<AssistantOwnKeyDailyQuota?> GetOwnQuotaAsync(Guid userId, DateOnly dayUtc, CancellationToken ct);
    Task<AssistantBudgetMonth?> GetLatestMonthAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct);
    Task<decimal> GetCommittedAndReservedAmountAsync(Guid monthId, AssistantKeyFunding funding, CancellationToken ct);
    Task<decimal> GetCommittedCostAsync(Guid monthId, AssistantKeyFunding funding, CancellationToken ct);
    Task<IReadOnlyList<AssistantFinancialCall>> ListCallsAsync(Guid operationId, CancellationToken ct);
    void Add(AssistantFinancialOperation operation);
    void Add(AssistantPlatformKeyDailyQuota quota);
    void Add(AssistantOwnKeyDailyQuota quota);
    void Add(AssistantBudgetMonth month);
    void Add(AssistantFinancialCall call);
    void Add(AssistantUsageFact fact);
}
