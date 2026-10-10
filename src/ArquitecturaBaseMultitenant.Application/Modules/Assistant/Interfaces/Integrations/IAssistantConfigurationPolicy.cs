using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

public interface IAssistantConfigurationPolicy
{
    Task<Result<AssistantRuntimeConfiguration>> ResolveAsync(CancellationToken cancellationToken);
}
