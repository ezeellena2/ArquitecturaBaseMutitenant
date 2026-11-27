namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// La vista previa de unos filtros sin guardar, con la persona o el cliente de ejemplo como destinatario. Trae hasta 200 opciones;
/// <see cref="Total"/> es la cantidad exacta y <see cref="ExceedsLimit"/> avisa que pasan el tope.
/// </summary>
public sealed record FormCatalogPreviewResponse(IReadOnlyList<FormCatalogPreviewOption> Options, int Total, bool ExceedsLimit);

public sealed record FormCatalogPreviewOption(string Text, string? Detail);
