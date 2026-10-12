using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantBudgetAlertRepository
{
    Task<AssistantBudgetAlert?> FindAsync(Guid monthId, AssistantKeyFunding funding, int threshold, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListPendingAsync(CancellationToken ct);
    Task<AssistantBudgetAlert?> ClaimAsync(Guid id, CancellationToken ct);
    void Add(AssistantBudgetAlert alert);
}
