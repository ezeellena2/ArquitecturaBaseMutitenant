using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

public interface IAssistantProviderCatalog
{
    IReadOnlyList<AssistantProviderReference> All { get; }
    IReadOnlyList<AssistantProviderReference> InitialModels { get; }
    AssistantProviderReference? Find(string providerModelId);
}
