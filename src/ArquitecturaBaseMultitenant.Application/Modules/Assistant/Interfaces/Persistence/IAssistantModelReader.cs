using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantModelReader
{
    Task<IReadOnlyList<AssistantModelPriceResponse>> ListAllAsync(CancellationToken ct);
    Task<PagedResult<AssistantModelPriceResponse>> ListAsync(AssistantModelListRequest request, CancellationToken ct);
}
