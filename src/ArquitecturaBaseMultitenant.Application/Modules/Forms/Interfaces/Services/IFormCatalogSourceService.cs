using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;

/// <summary>
/// Las fuentes de los catálogos con filtros (E12, §4 y §4b): la definición de «Miembros», sus roles y empresas, las etiquetas de
/// los contactos, las personas y los clientes de ejemplo, y la vista previa. Solo lee.
/// </summary>
public interface IFormCatalogSourceService
{
    Task<Result<FormCatalogSourceResponse>> GetMembersSourceAsync(CancellationToken cancellationToken);
    Task<Result<PagedResult<FormCatalogReference>>> ListRolesAsync(ListFormCatalogLookupsRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<FormCatalogReference>>> ListCompaniesAsync(ListFormCatalogLookupsRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<FormCatalogTagRow>>> ListContactTagsAsync(ListFormCatalogLookupsRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<FormCatalogSampleMemberRow>>> ListSampleMembersAsync(ListFormCatalogLookupsRequest request,
        CancellationToken cancellationToken);
    Task<Result<PagedResult<FormCatalogSampleContactRow>>> ListSampleContactsAsync(ListFormCatalogLookupsRequest request,
        CancellationToken cancellationToken);
    Task<Result<FormCatalogPreviewResponse>> PreviewAsync(FormCatalogPreviewRequest request, CancellationToken cancellationToken);
}
