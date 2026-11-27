using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;

/// <summary>
/// La carga de una vez de un catálogo a mano (C7 a C10): revisar lo pegado o un archivo sin escribir, aplicarlo en una UoW,
/// exportar sus opciones y descargar la plantilla. La revisión y aplicar llegan con la barrera de la persona ya tomada por el
/// filtro de la ruta (C11); exportar la toma acá.
/// </summary>
public interface IFormCatalogImportService
{
    Task<Result<FormCatalogImportPreviewResponse>> PreviewAsync(FormCatalogImportPreviewRequest request, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> ApplyAsync(ApplyFormCatalogImportRequest request, CancellationToken cancellationToken);
    Task<Result<FormCatalogFileResponse>> ExportAsync(Guid catalogId, CancellationToken cancellationToken);
    Task<Result<FormCatalogFileResponse>> GetTemplateAsync(FormCatalogTemplateRequest request, CancellationToken cancellationToken);
}
