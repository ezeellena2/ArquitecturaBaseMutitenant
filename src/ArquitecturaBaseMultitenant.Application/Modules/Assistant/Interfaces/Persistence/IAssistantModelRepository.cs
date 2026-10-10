using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantModelRepository
{
    Task<AssistantModelPrice?> GetAsync(Guid id, uint expectedVersion, CancellationToken ct);
    Task<bool> ExistsAsync(string provider, string providerModelId, Guid? exceptId, CancellationToken ct);
    void Add(AssistantModelPrice model);
    void MarkUpdated(AssistantModelPrice model);
}
