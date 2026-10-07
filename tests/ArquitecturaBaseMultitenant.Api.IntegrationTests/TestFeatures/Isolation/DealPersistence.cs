using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

internal interface IDealReader
{
    Task<PagedResult<DealResponse>> ListAsync(IsolationListRequest request, CancellationToken cancellationToken);
    Task<DealResponse?> FindAsync(Guid id, CancellationToken cancellationToken);
}

internal interface IDealRepository
{
    Task<Deal?> GetAsync(Guid id, uint version, CancellationToken cancellationToken);
    void Add(Deal deal);
    void MarkUpdated(Deal deal);
}

internal sealed class DealReader(ApplicationDbContext context) : IDealReader
{
    public Task<PagedResult<DealResponse>> ListAsync(IsolationListRequest request, CancellationToken cancellationToken) =>
        Project(Query().OrderBy(row => row.Id)).ToPagedResultAsync(request, cancellationToken);

    public Task<DealResponse?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Project(Query().Where(row => row.Id == id)).SingleOrDefaultAsync(cancellationToken);

    // No se une con la cuenta ni el espacio personal: la empresa recibe exclusivamente la copia.
    private IQueryable<Deal> Query() => context.Set<Deal>().AsNoTracking();

    private static IQueryable<DealResponse> Project(IQueryable<Deal> query) => query
        .Select(deal => new DealResponse(deal.Id, deal.Summary, deal.ConsumerName, deal.Status, deal.Version));
}

internal sealed class DealRepository(ApplicationDbContext context, ITenantContext tenant) : IDealRepository
{
    public async Task<Deal?> GetAsync(Guid id, uint version, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenant.RequiredTenantId;
        var deal = await context.Set<Deal>().SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (deal is not null) context.Entry(deal).Property(row => row.Version).OriginalValue = version;
        return deal;
    }

    public void Add(Deal deal)
    {
        context.RequireTransaction();
        _ = tenant.RequiredTenantId;
        context.Set<Deal>().Add(deal);
    }

    public void MarkUpdated(Deal deal)
    {
        context.RequireTransaction();
        _ = tenant.RequiredTenantId;
        context.Entry(deal).State = EntityState.Modified;
    }
}

internal interface IPosterReader
{
    Task<PagedResult<PosterResponse>> ListPublishedAsync(Guid businessTenantId, IsolationListRequest request, CancellationToken cancellationToken);
    Task<PosterResponse?> FindPublishedAsync(Guid businessTenantId, Guid id, CancellationToken cancellationToken);
}

internal sealed class PosterReader(ApplicationDbContext context) : IPosterReader
{
    public Task<PagedResult<PosterResponse>> ListPublishedAsync(Guid businessTenantId, IsolationListRequest request, CancellationToken cancellationToken) =>
        Project(Query(businessTenantId).OrderBy(row => row.Id)).ToPagedResultAsync(request, cancellationToken);

    public Task<PosterResponse?> FindPublishedAsync(Guid businessTenantId, Guid id, CancellationToken cancellationToken) =>
        Project(Query(businessTenantId).Where(row => row.Id == id)).SingleOrDefaultAsync(cancellationToken);

    // El filtro Public también deja ver borradores propios a Business; la lectura pública siempre exige publicación.
    private IQueryable<Poster> Query(Guid businessTenantId) => context.Set<Poster>().AsNoTracking()
        .Where(row => row.BusinessTenantId == businessTenantId && row.IsPublished);

    private static IQueryable<PosterResponse> Project(IQueryable<Poster> query) => query
        .Select(row => new PosterResponse(row.Id));
}
