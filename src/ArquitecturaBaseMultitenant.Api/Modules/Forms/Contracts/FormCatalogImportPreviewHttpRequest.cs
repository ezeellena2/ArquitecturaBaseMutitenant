using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>
/// La revisión de un archivo o de lo pegado, que viaja como un CSV con las columnas pegadas (C7): <c>mode</c> es append al pegar y
/// replace con un archivo; <c>page</c> y <c>pageSize</c> paginan las cuatro listas.
/// </summary>
public sealed record FormCatalogImportPreviewHttpRequest(IFormFile? File, FormCatalogImportMode? Mode, uint? Version, int? Page, int? PageSize)
{
    public override string ToString() => nameof(FormCatalogImportPreviewHttpRequest);
}
