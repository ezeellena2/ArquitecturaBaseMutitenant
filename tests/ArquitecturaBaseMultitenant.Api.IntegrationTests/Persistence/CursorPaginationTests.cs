using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class CursorPaginationTests(ApiFactory factory)
{
    [Fact]
    public async Task Inserting_a_newer_row_between_pages_neither_repeats_nor_skips_older_rows()
    {
        var tenantId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, tenantId, cancellationToken);
        var baseline = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var ids = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };
        for (var index = 0; index < ids.Length; index++)
        {
            await fixture.AddAsync(tenantId, ids[index], $"Fila {index}", baseline.AddMinutes(-index), cancellationToken);
        }

        var first = await fixture.Rows.ToCursorResultAsync(
            new WidgetCursorRequest { Limit = 2 }, widget => widget.CreatedAtUtc, widget => widget.Id,
            cancellationToken);
        Assert.Equal(ids[..2], first.Items.Select(widget => widget.Id));
        Assert.True(first.HasMore);
        Assert.NotNull(first.NextCursor);

        await fixture.AddAsync(tenantId, Guid.CreateVersion7(), "Nueva", baseline.AddMinutes(1), cancellationToken);
        var second = await fixture.Rows.ToCursorResultAsync(
            new WidgetCursorRequest { Limit = 2, After = first.NextCursor },
            widget => widget.CreatedAtUtc, widget => widget.Id, cancellationToken);

        Assert.Equal(ids[2..], second.Items.Select(widget => widget.Id));
        Assert.False(second.HasMore);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task Equal_timestamps_use_id_as_a_stable_tie_breaker()
    {
        var tenantId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, tenantId, cancellationToken);
        var stampUtc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        foreach (var id in new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() })
        {
            await fixture.AddAsync(tenantId, id, "Mismo instante", stampUtc, cancellationToken);
        }

        var expected = await fixture.Rows.OrderByDescending(widget => widget.CreatedAtUtc)
            .ThenByDescending(widget => widget.Id).Select(widget => widget.Id).ToArrayAsync(cancellationToken);
        var collected = new List<Guid>();
        string? after = null;
        do
        {
            var page = await fixture.Rows.ToCursorResultAsync(
                new WidgetCursorRequest { Limit = 1, After = after },
                widget => widget.CreatedAtUtc, widget => widget.Id, cancellationToken);
            collected.AddRange(page.Items.Select(widget => widget.Id));
            after = page.NextCursor;
        } while (after is not null);

        Assert.Equal(expected, collected);
    }

    [Fact]
    public async Task Invalid_cursor_is_rejected_before_query_execution()
    {
        var tenantId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, tenantId, cancellationToken);

        await Assert.ThrowsAsync<InvalidCursorException>(() => fixture.Rows.ToCursorResultAsync(
            new WidgetCursorRequest { After = "YWJj" },
            widget => widget.CreatedAtUtc, widget => widget.Id, cancellationToken));
    }

    [Fact]
    public void Codec_round_trips_sort_value_and_id_and_rejects_modified_data()
    {
        var stampUtc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var id = Guid.CreateVersion7();
        var encoded = CursorCodec.Encode(stampUtc, id);

        Assert.True(CursorCodec.TryDecode(encoded, out var decoded));
        Assert.Equal(new CursorPosition(stampUtc, id), decoded);
        Assert.False(CursorCodec.TryDecode("YWJj", out _));
        Assert.False(CursorCodec.TryDecode(encoded + "!", out _));
        Assert.False(CursorCodec.TryDecode(null, out _));
    }

    private sealed record WidgetCursorRequest : CursorRequest;
}
