namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Una opción guardada tal como la comparan la revisión, la exportación y la acción del asistente, con su valor canónico (C6).
/// La proyecta el lector en la consulta, sin cargar la entidad.
/// </summary>
public sealed record FormCatalogStoredItemRow(Guid Id, string Code, string CodeNormalized, string Text, string? Detail, string? Value,
    int Position, bool IsActive)
{
    public override string ToString() => nameof(FormCatalogStoredItemRow);
}
