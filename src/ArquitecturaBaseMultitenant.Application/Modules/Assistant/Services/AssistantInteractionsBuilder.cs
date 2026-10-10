using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Engagement;
using ArquitecturaBaseMultitenant.Application.Models.Engagement;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Reads bounded contributions. Each source derives and authorizes its own current consumer actor.</summary>
internal sealed class AssistantInteractionsBuilder(IEnumerable<IConsumerInteractionSource> sources)
{
    private readonly IReadOnlyList<IConsumerInteractionSource> _sources = sources.OrderBy(source => source.Key, StringComparer.Ordinal).ToArray();
    internal async Task<Result<PagedResult<ConsumerInteractionResponse>>> ListAsync(ListConsumerInteractionsRequest request, CancellationToken ct)
    {
        var firstPages = new List<(IConsumerInteractionSource Source, PagedResult<ConsumerInteractionResponse> Page)>();
        foreach (var source in _sources)
        {
            var page = await source.ListAsync(request with { Page = 1 }, ct);
            if (page.IsFailure) return page.Error;
            firstPages.Add((source, page.Value));
        }
        // Stable concatenation by source key permits global paging without extracting entire sources.
        var total = firstPages.Sum(item => item.Page.TotalCount);
        var skip = (request.Page - 1) * request.PageSize;
        var items = new List<ConsumerInteractionResponse>();
        foreach (var (source, first) in firstPages)
        {
            if (skip >= first.TotalCount) { skip -= first.TotalCount; continue; }
            var sourcePage = skip / request.PageSize + 1;
            var withinPage = skip % request.PageSize;
            while (items.Count < request.PageSize)
            {
                var page = sourcePage == 1 ? Result.Success(first)
                    : await source.ListAsync(request with { Page = sourcePage }, ct);
                if (page.IsFailure) return page.Error;
                items.AddRange(page.Value.Items.Skip(withinPage).Take(request.PageSize - items.Count));
                if (!page.Value.HasNext) break;
                sourcePage++;
                withinPage = 0;
            }
            skip = 0;
            if (items.Count == request.PageSize) break;
        }
        return new PagedResult<ConsumerInteractionResponse>(items, request.Page, request.PageSize, total);
    }
}
