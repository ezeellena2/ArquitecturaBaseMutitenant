using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantModelService
{
    Task<Result<PagedResult<AssistantModelPriceResponse>>> ListAsync(AssistantModelListRequest request, CancellationToken cancellationToken);
    Task<Result<AssistantCreatedModelResponse>> CreateAsync(CreateAssistantModelRequest request, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UpdateAsync(Guid id, UpdateAssistantModelRequest request, CancellationToken cancellationToken);
}
