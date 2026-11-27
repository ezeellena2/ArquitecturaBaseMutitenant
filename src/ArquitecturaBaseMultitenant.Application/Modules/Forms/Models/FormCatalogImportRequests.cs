using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Revisar un archivo o lo pegado, que viaja como un CSV con las columnas pegadas (C7), contra la versión que tiene la pantalla.
/// <c>Page</c> y <c>PageSize</c> paginan las cuatro listas de la revisión.
/// </summary>
public sealed record FormCatalogImportPreviewRequest : PagedRequest
{
    public Guid CatalogId { get; init; }
    public string? FileName { get; init; }
    public Stream? Content { get; init; }
    public FormCatalogImportMode? Mode { get; init; }
    public uint? Version { get; init; }

    public override string ToString() => nameof(FormCatalogImportPreviewRequest);
}

/// <summary>
/// Aplicar lo revisado: los mismos bytes, su SHA-256 y la versión de la revisión. <c>SkipErrorRows</c> aplica sin las filas con
/// errores, y <c>ConfirmedDeactivations</c> es la cantidad exacta de bajas que se confirmó (C7).
/// </summary>
public sealed record ApplyFormCatalogImportRequest(Guid CatalogId, string? FileName, Stream? Content, FormCatalogImportMode? Mode,
    string? Sha256, uint? Version, bool? SkipErrorRows, int? ConfirmedDeactivations)
{
    public override string ToString() => nameof(ApplyFormCatalogImportRequest);
}

/// <summary>La plantilla para importar, con la columna del valor del tipo pedido; sin tipo, la de un catálogo de texto (C10).</summary>
public sealed record FormCatalogTemplateRequest(FormCatalogValueType? ValueType);

/// <summary>Una planilla para descargar: la exportación o la plantilla.</summary>
public sealed record FormCatalogFileResponse(Stream Content, string MediaType, string FileName)
{
    public override string ToString() => nameof(FormCatalogFileResponse);
}

/// <summary>Una opción guardada tal como la comparan la revisión y la exportación, con su valor canónico (C6).</summary>
public sealed record FormCatalogItemSnapshot(Guid Id, string Code, string CodeNormalized, string Text, string? Detail, string? Value,
    int Position, bool IsActive)
{
    public override string ToString() => nameof(FormCatalogItemSnapshot);
}
