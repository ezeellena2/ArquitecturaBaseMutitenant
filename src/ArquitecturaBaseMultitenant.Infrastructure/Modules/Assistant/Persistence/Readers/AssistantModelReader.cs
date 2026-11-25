using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;

internal sealed class AssistantModelReader(ApplicationDbContext context) : IAssistantModelReader
{
    // 0064 §6: un modelo de un proveedor que esta versión no conoce no se ofrece ni se lee como uno conocido.
    private static readonly string[] KnownProviders = Enum.GetNames<AssistantProvider>();

    public async Task<IReadOnlyList<AssistantModelPriceResponse>> ListAllAsync(CancellationToken ct) =>
        await Known().OrderBy(value => value.DisplayName).ThenBy(value => value.Id)
            .Select(value => AssistantModelReferenceBuilder.WithRevision(new AssistantModelPriceResponse(value.Id, value.DisplayName, Enum.Parse<AssistantProvider>(value.Provider),
                value.ProviderModelId, value.InputPrice, value.OutputPrice, value.IsEnabled, value.Version,
                AssistantModelReferenceBuilder.ToResponse(value.Technical)))).ToArrayAsync(ct);

    public async Task<PagedResult<AssistantModelPriceResponse>> ListAsync(AssistantModelListRequest request, CancellationToken ct)
    {
        var query = Known().ApplySearch(request.Search, value => value.DisplayName, value => value.ProviderModelId);
        if (request.Active is { } active) query = query.Where(value => value.IsEnabled == active);
        return await query.OrderBy(value => value.DisplayName).ThenBy(value => value.Id)
            .Select(value => AssistantModelReferenceBuilder.WithRevision(new AssistantModelPriceResponse(value.Id, value.DisplayName, Enum.Parse<AssistantProvider>(value.Provider),
                value.ProviderModelId, value.InputPrice, value.OutputPrice, value.IsEnabled, value.Version,
                AssistantModelReferenceBuilder.ToResponse(value.Technical))))
            .ToPagedResultAsync(new CatalogPage { Page = request.Page ?? 1, PageSize = request.PageSize ?? AssistantTechnicalLimits.QuickRows }, ct);
    }
    public Task<string?> FindProviderAsync(Guid modelId, CancellationToken ct) =>
        context.Set<AssistantModelPrice>().AsNoTracking().Where(value => value.Id == modelId).Select(value => value.Provider).SingleOrDefaultAsync(ct);

    private IQueryable<AssistantModelPrice> Known() => context.Set<AssistantModelPrice>().AsNoTracking().Where(value => KnownProviders.Contains(value.Provider));
    private sealed record CatalogPage : PagedRequest;
}
