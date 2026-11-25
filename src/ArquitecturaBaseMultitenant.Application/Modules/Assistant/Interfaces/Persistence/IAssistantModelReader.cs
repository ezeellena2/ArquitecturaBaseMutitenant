using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantModelReader
{
    Task<IReadOnlyList<AssistantModelPriceResponse>> ListAllAsync(CancellationToken ct);
    Task<PagedResult<AssistantModelPriceResponse>> ListAsync(AssistantModelListRequest request, CancellationToken ct);
    /// <summary>El proveedor guardado de un modelo, tal como está, también uno que esta versión no conoce (0064 §6); null si no existe.</summary>
    Task<string?> FindProviderAsync(Guid modelId, CancellationToken ct);
}
