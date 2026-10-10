using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantModelService(IAssistantModelReader reader, AssistantModelWriter writer, IUnitOfWork unitOfWork,
    IRequestValidator validator, ICurrencyCatalog currencies, TimeProvider clock, ILogger<AssistantModelService> logger) : IAssistantModelService
{
    public Task<Result<PagedResult<AssistantModelPriceResponse>>> ListAsync(AssistantModelListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<AssistantModelPriceResponse>>(logger, clock, "AssistantModelList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await reader.ListAsync(request, cancellationToken);
        });

    public Task<Result<AssistantCreatedModelResponse>> CreateAsync(CreateAssistantModelRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<AssistantCreatedModelResponse>(logger, clock, "AssistantModelCreate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await currencies.FindAsync(request.InputPricePerMillion!.Currency.Value, cancellationToken) is not { IsEnabled: true }) return CurrencyCodeErrors.Invalid;
            try
            {
                var result = await unitOfWork.ExecuteInTransactionAsync(ct => writer.CreateAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
                return result.IsFailure ? result.Error : new AssistantCreatedModelResponse(result.Value.Id, result.Value.Version);
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueKeys.AssistantModel) { return AssistantErrors.ModelAlreadyExists; }
        });

    public Task<Result<VersionResponse>> UpdateAsync(Guid id, UpdateAssistantModelRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "AssistantModelUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await currencies.FindAsync(request.InputPricePerMillion!.Currency.Value, cancellationToken) is not { IsEnabled: true }) return CurrencyCodeErrors.Invalid;
            try
            {
                var result = await unitOfWork.ExecuteInTransactionAsync(ct => writer.UpdateAsync(id, request, ct), CommitPolicy.OnSuccess, cancellationToken);
                return VersionResponse.Of(result);
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueKeys.AssistantModel) { return AssistantErrors.ModelAlreadyExists; }
        });
}
