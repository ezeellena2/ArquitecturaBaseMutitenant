using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

public interface IPersonalWidgetService
{
    Task<Result<PagedResult<PersonalWidgetResponse>>> ListAsync(ListPersonalWidgetsRequest request, CancellationToken cancellationToken);
    Task<Result<PersonalWidgetResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<Guid>> CreateAsync(CreatePersonalWidgetRequest request, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UpdateAsync(UpdatePersonalWidgetRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(DeletePersonalWidgetRequest request, CancellationToken cancellationToken);
}

/// <summary>CRUD privado sintético con la misma validación, UoW y versión de Roles; no concede permisos.</summary>
internal sealed class PersonalWidgetService(IPersonalWidgetReader reader, IPersonalWidgetRepository repository,
    IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<PersonalWidgetService> logger)
    : IPersonalWidgetService
{
    public Task<Result<PagedResult<PersonalWidgetResponse>>> ListAsync(ListPersonalWidgetsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<PersonalWidgetResponse>>(logger, clock, "ListPersonalTestWidgets", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await reader.ListAsync(request, cancellationToken);
        });

    public Task<Result<PersonalWidgetResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PersonalWidgetResponse>(logger, clock, "GetPersonalTestWidget", async () =>
            await reader.FindAsync(id, cancellationToken) is { } row ? row : NotFound);

    public Task<Result<Guid>> CreateAsync(CreatePersonalWidgetRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<Guid>(logger, clock, "CreatePersonalTestWidget", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(ct =>
            {
                var widget = new Widget(request.Name!);
                repository.Add(widget);
                return Task.FromResult(Result.Success(widget.Id));
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<VersionResponse>> UpdateAsync(UpdatePersonalWidgetRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "UpdatePersonalTestWidget", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var widget = await repository.GetAsync(request.Id, request.Version!.Value, ct);
                if (widget is null) return Result.Failure<Widget>(NotFound);
                var changed = widget.Rename(request.Name!);
                if (changed.IsFailure) return Result.Failure<Widget>(changed.Error);
                repository.MarkUpdated(widget);
                return Result.Success(widget);
            }, CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(saved);
        });

    public Task<Result> DeleteAsync(DeletePersonalWidgetRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "DeletePersonalTestWidget", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var widget = await repository.GetAsync(request.Id, request.Version!.Value, ct);
                if (widget is null) return Result.Failure(NotFound);
                repository.Remove(widget);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    internal static Error NotFound => Error.NotFound("Http.NotFound", "The resource was not found.");
}
