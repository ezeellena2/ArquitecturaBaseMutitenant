using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

/// <summary>Agregaciones técnicas; la futura gestión HTTP autoriza su caso de uso antes de invocarlas.</summary>
public interface IAssistantFinanceReader
{
    Task<IReadOnlyList<AssistantFinancialAggregateRow>> ListPlatformAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct);
    Task<IReadOnlyList<AssistantFinancialAggregateRow>> ListBusinessAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct);
    Task<AssistantDailyQuotaUsageRow> FindDailyQuotaAsync(Guid userId, AssistantMode mode, DateOnly dayUtc, CancellationToken ct);
    Task<AssistantBudgetMonthRow?> FindLatestMonthAsync(DateTime nowUtc, CancellationToken ct);
    Task<AssistantMonthCostRow> FindMonthCostAsync(Guid monthId, AssistantKeyFunding funding, CancellationToken ct);
}
