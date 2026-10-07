using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

public interface IDealService
{
    Task<Result<PagedResult<DealResponse>>> ListAsync(IsolationListRequest request, CancellationToken cancellationToken);
    Task<Result<DealResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<Guid>> CreateAsync(CreateDealRequest request, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> TransitionAsync(TransitionDealRequest request, CancellationToken cancellationToken);
}

/// <summary>Receta del módulo: una UoW, copia mínima, dominio decide la parte y los interceptores completan el resto.</summary>
internal sealed class DealService(IDealReader reader, IDealRepository repository, IUserRepository accounts,
    ICurrentUser user, ITenantContext tenant, IPublicSiteContext publicSite, IRequestValidator validator,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<DealService> logger) : IDealService
{
    public Task<Result<PagedResult<DealResponse>>> ListAsync(IsolationListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<DealResponse>>(logger, clock, "ListTestDeals", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await reader.ListAsync(request, cancellationToken);
        });

    public Task<Result<DealResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<DealResponse>(logger, clock, "GetTestDeal", async () =>
            await reader.FindAsync(id, cancellationToken) is { } row ? row : NotFound);

    public Task<Result<Guid>> CreateAsync(CreateDealRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<Guid>(logger, clock, "CreateTestDeal", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (publicSite.BusinessTenantId is not { } businessId) return PublicPageErrors.NotFound;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var account = await accounts.GetByIdAsync(user.UserId!.Value, ct);
                if (account is null) return Result.Failure<Guid>(NotFound);
                var deal = Deal.Request(businessId, request.Summary!, account.DisplayName);
                repository.Add(deal);
                return Result.Success(deal.Id);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<VersionResponse>> TransitionAsync(TransitionDealRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "TransitionTestDeal", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var deal = await repository.GetAsync(request.Id, request.Version!.Value, ct);
                if (deal is null) return Result.Failure<Deal>(NotFound);
                var party = user.Access == Access.Consumer ? Party.Consumer : Party.Business;
                var changed = deal.Transition(request.Status, party, tenant.RequiredTenantId);
                if (changed.IsFailure) return Result.Failure<Deal>(changed.Error);
                repository.MarkUpdated(deal);
                return Result.Success(deal);
            }, CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(saved);
        });

    internal static Error NotFound => Error.NotFound("Http.NotFound", "The resource was not found.");
}

public interface IPosterService
{
    Task<Result<PagedResult<PosterResponse>>> ListAsync(IsolationListRequest request, CancellationToken cancellationToken);
    Task<Result<PosterResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
}

internal sealed class PosterService(IPosterReader reader, IPublicSiteContext publicSite, IRequestValidator validator, TimeProvider clock,
    ILogger<PosterService> logger) : IPosterService
{
    public Task<Result<PagedResult<PosterResponse>>> ListAsync(IsolationListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<PosterResponse>>(logger, clock, "ListTestPosters", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await reader.ListPublishedAsync(publicSite.BusinessTenantId!.Value, request, cancellationToken);
        });

    public Task<Result<PosterResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PosterResponse>(logger, clock, "GetTestPoster", async () =>
            await reader.FindPublishedAsync(publicSite.BusinessTenantId!.Value, id, cancellationToken)
                is { } row ? row : DealService.NotFound);
}
