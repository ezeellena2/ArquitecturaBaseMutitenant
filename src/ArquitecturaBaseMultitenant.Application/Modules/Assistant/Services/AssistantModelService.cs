using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantModelService(TimeProvider clock, ILogger<AssistantModelService> logger) : IAssistantModelService
{
    public Task<Result<PagedResult<AssistantModelPriceResponse>>> ListAsync(AssistantModelListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantModelList", () => Task.FromResult(Result.Failure<PagedResult<AssistantModelPriceResponse>>(ContractErrors.NotImplemented)));

    public Task<Result<AssistantCreatedModelResponse>> CreateAsync(CreateAssistantModelRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantModelCreate", () => Task.FromResult(Result.Failure<AssistantCreatedModelResponse>(ContractErrors.NotImplemented)));

    public Task<Result<VersionResponse>> UpdateAsync(Guid id, UpdateAssistantModelRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantModelUpdate", () => Task.FromResult(Result.Failure<VersionResponse>(ContractErrors.NotImplemented)));
}
