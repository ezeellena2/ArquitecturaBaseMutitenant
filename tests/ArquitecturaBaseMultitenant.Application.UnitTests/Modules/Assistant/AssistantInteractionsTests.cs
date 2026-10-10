using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Engagement;
using ArquitecturaBaseMultitenant.Application.Models.Engagement;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantInteractionsTests
{
    [Fact]
    public async Task Aggregation_pages_across_sources_without_fetching_entire_contributions()
    {
        var first = new Source("alpha", 25);
        var second = new Source("beta", 18);
        var builder = new AssistantInteractionsBuilder([second, first]);
        var page = await builder.ListAsync(new() { Page = 2, PageSize = 20 }, TestContext.Current.CancellationToken);
        Assert.True(page.IsSuccess);
        Assert.Equal(43, page.Value.TotalCount);
        Assert.Equal(20, page.Value.Items.Count);
        Assert.Equal(Enumerable.Range(20, 5).Select(index => "alpha" + index).Concat(Enumerable.Range(0, 15).Select(index => "beta" + index)),
            page.Value.Items.Select(row => row.Summary));
        Assert.True(page.Value.HasNext);
        Assert.Equal(new[] { 1, 2 }, first.Requests);
        Assert.Equal(new[] { 1 }, second.Requests);
        var last = await builder.ListAsync(new() { Page = 3, PageSize = 20 }, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "beta15", "beta16", "beta17" }, last.Value.Items.Select(row => row.Summary));
        Assert.False(last.Value.HasNext);
    }

    private sealed class Source(string key, int count) : IConsumerInteractionSource
    {
        internal List<int> Requests { get; } = [];
        public string Key => key;
        public Task<Result<PagedResult<ConsumerInteractionResponse>>> ListAsync(ListConsumerInteractionsRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request.Page);
            var start = (request.Page - 1) * request.PageSize;
            var rows = Enumerable.Range(0, count).Skip(start).Take(request.PageSize)
                .Select(index => new ConsumerInteractionResponse(Guid.NewGuid(), key, Guid.NewGuid(), "Business", key + index,
                    new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc))).ToArray();
            return Task.FromResult(Result.Success(new PagedResult<ConsumerInteractionResponse>(rows, request.Page, request.PageSize, count)));
        }
    }
}

