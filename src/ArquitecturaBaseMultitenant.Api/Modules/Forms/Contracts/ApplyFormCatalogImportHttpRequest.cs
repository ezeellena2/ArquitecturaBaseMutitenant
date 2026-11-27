using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>
/// Aplicar lo revisado: el mismo archivo, su SHA-256 y la versión de la revisión. <c>skipErrorRows</c> aplica sin las filas con
/// errores, y <c>confirmedDeactivations</c> es la cantidad exacta de bajas que se confirmó (C7).
/// </summary>
public sealed record ApplyFormCatalogImportHttpRequest(IFormFile? File, FormCatalogImportMode? Mode, string? Sha256, uint? Version,
    bool? SkipErrorRows, int? ConfirmedDeactivations)
{
    public override string ToString() => nameof(ApplyFormCatalogImportHttpRequest);
}
