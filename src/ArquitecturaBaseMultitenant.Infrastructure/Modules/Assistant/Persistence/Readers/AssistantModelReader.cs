using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;

internal sealed class AssistantModelReader(ApplicationDbContext context) : IAssistantModelReader
{
    public async Task<IReadOnlyList<AssistantModelPriceResponse>> ListAllAsync(CancellationToken ct) =>
        await context.Set<AssistantModelPrice>().AsNoTracking().OrderBy(value => value.DisplayName).ThenBy(value => value.Id)
            .Select(value => new AssistantModelPriceResponse(value.Id, value.DisplayName, Enum.Parse<AssistantProvider>(value.Provider),
                value.ProviderModelId, value.InputPrice, value.OutputPrice, value.IsEnabled, value.Version)).ToArrayAsync(ct);

    public async Task<PagedResult<AssistantModelPriceResponse>> ListAsync(AssistantModelListRequest request, CancellationToken ct)
    {
        var query = context.Set<AssistantModelPrice>().AsNoTracking().ApplySearch(request.Search, value => value.DisplayName, value => value.ProviderModelId);
        if (request.Active is { } active) query = query.Where(value => value.IsEnabled == active);
        return await query.OrderBy(value => value.DisplayName).ThenBy(value => value.Id)
            .Select(value => new AssistantModelPriceResponse(value.Id, value.DisplayName, Enum.Parse<AssistantProvider>(value.Provider),
                value.ProviderModelId, value.InputPrice, value.OutputPrice, value.IsEnabled, value.Version))
            .ToPagedResultAsync(new CatalogPage { Page = request.Page ?? 1, PageSize = request.PageSize ?? 20 }, ct);
    }
    private sealed record CatalogPage : PagedRequest;
}
