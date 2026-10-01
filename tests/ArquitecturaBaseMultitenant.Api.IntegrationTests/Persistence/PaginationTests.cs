using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba páginas estables, totales y desempates por identificador. Mantiene el total real aunque se
/// solicite una página fuera del rango.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PaginationTests(ApiFactory factory)
{
    [Fact]
    public async Task Equal_sort_values_use_id_to_keep_pages_disjoint_and_stable()
    {
        var tenantId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, tenantId, cancellationToken);
        var ids = Enumerable.Range(1, 15).Select(_ => Guid.CreateVersion7()).ToArray();
        foreach (var id in ids)
        {
            await fixture.AddAsync(tenantId, id, "Mismo nombre",
                new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc), cancellationToken);
        }

        var sorted = fixture.Rows.ApplySort(new SortDescriptor("name", false),
            WidgetQueryFixture.SortableFields, new SortDescriptor("name", false), widget => widget.Id);
        var first = await sorted.ToPagedResultAsync(new WidgetPageRequest { Page = 1 }, cancellationToken);
        var second = await sorted.ToPagedResultAsync(new WidgetPageRequest { Page = 2 }, cancellationToken);

        Assert.Equal(15, first.TotalCount);
        Assert.Equal(15, second.TotalCount);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(5, second.Items.Count);
        var expected = await fixture.Rows.OrderBy(widget => widget.Id)
            .Select(widget => widget.Id).ToArrayAsync(cancellationToken);
        Assert.Equal(expected, first.Items.Concat(second.Items).Select(widget => widget.Id));
    }

    [Fact]
    public async Task Out_of_range_page_is_empty_but_keeps_the_real_total()
    {
        var tenantId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, tenantId, cancellationToken);
        await fixture.AddAsync(tenantId, Guid.CreateVersion7(), "Uno",
            new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc), cancellationToken);

        var page = await fixture.Rows.OrderBy(widget => widget.Name)
            .ToPagedResultAsync(new WidgetPageRequest { Page = 8 }, cancellationToken);

        Assert.Empty(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(8, page.Page);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public void Sort_outside_reader_map_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new[] { 1 }.AsQueryable().ApplySort(new SortDescriptor("hidden", false),
                new SortMap<int> { ["value"] = value => value }, new SortDescriptor("value", false),
                value => value));
    }

    private sealed record WidgetPageRequest : PagedRequest;
}
