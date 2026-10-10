using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

/// <summary>Agregaciones técnicas; la futura gestión HTTP autoriza su caso de uso antes de invocarlas.</summary>
public interface IAssistantFinanceReader
{
    Task<IReadOnlyList<AssistantFinancialAggregateRow>> ListPlatformAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct);
    Task<IReadOnlyList<AssistantFinancialAggregateRow>> ListBusinessAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct);
}
