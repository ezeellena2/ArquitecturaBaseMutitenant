using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;

/// <summary>Los catálogos de la organización (E12): el listado, el detalle, sus opciones, el alta y guardar todo junto (C3).</summary>
public interface IFormCatalogService
{
    Task<Result<PagedResult<FormCatalogRow>>> ListAsync(ListFormCatalogsRequest request, CancellationToken cancellationToken);
    Task<Result<FormCatalogFilterCountsResponse>> GetFilterCountsAsync(CancellationToken cancellationToken);
    Task<Result<FormCatalogResponse>> GetAsync(Guid catalogId, CancellationToken cancellationToken);
    Task<Result<PagedResult<FormCatalogItemRow>>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request,
        CancellationToken cancellationToken);
    Task<Result<FormCatalogCreatedResponse>> CreateAsync(CreateFormCatalogRequest request, CancellationToken cancellationToken);
    Task<Result<FormCatalogUpdatedResponse>> UpdateAsync(UpdateFormCatalogRequest request, CancellationToken cancellationToken);
}
