using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

internal interface IPersonalWidgetReader
{
    Task<PagedResult<PersonalWidgetResponse>> ListAsync(ListPersonalWidgetsRequest request, CancellationToken cancellationToken);
    Task<PersonalWidgetResponse?> FindAsync(Guid id, CancellationToken cancellationToken);
}

internal interface IPersonalWidgetRepository
{
    Task<Widget?> GetAsync(Guid id, uint version, CancellationToken cancellationToken);
    void Add(Widget widget);
    void MarkUpdated(Widget widget);
    void Remove(Widget widget);
}

internal sealed class PersonalWidgetReader(ApplicationDbContext context) : IPersonalWidgetReader
{
    private static readonly SortMap<Widget> Sort = new()
    {
        ["name"] = row => row.Name,
        ["createdAtUtc"] = row => row.CreatedAtUtc,
    };

    public Task<PagedResult<PersonalWidgetResponse>> ListAsync(ListPersonalWidgetsRequest request, CancellationToken cancellationToken) =>
        Project(Query().ApplySearch(request.Search, row => row.Name)
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, new SortDescriptor("name", Descending: false), row => row.Id))
            .ToPagedResultAsync(request, cancellationToken);

    public Task<PersonalWidgetResponse?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Project(Query().Where(row => row.Id == id)).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<Widget> Query() => context.Set<Widget>().AsNoTracking();

    private static IQueryable<PersonalWidgetResponse> Project(IQueryable<Widget> query) => query
        .Select(row => new PersonalWidgetResponse(row.Id, row.Name, row.Version));
}

internal sealed class PersonalWidgetRepository(ApplicationDbContext context, ITenantContext tenant)
    : IPersonalWidgetRepository
{
    public async Task<Widget?> GetAsync(Guid id, uint version, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenant.RequiredTenantId;
        var widget = await context.Set<Widget>().SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (widget is not null) context.Entry(widget).Property(row => row.Version).OriginalValue = version;
        return widget;
    }

    public void Add(Widget widget)
    {
        context.RequireTransaction();
        _ = tenant.RequiredTenantId;
        context.Set<Widget>().Add(widget);
    }

    public void MarkUpdated(Widget widget)
    {
        context.RequireTransaction();
        _ = tenant.RequiredTenantId;
        context.Entry(widget).State = EntityState.Modified;
    }

    public void Remove(Widget widget)
    {
        context.RequireTransaction();
        _ = tenant.RequiredTenantId;
        context.Set<Widget>().Remove(widget);
    }
}
