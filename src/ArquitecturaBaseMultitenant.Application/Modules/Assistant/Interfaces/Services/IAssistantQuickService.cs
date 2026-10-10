using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantQuickService
{
    Task<Result<IReadOnlyList<AssistantQuickQueryResponse>>> ListAsync(CancellationToken cancellationToken);
    Task<Result<AssistantQuickCompositionResponse>> ExecuteAsync(string key, CancellationToken cancellationToken);
}
