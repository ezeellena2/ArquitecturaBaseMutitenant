namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Una opción de un catálogo a mano, por posición. <see cref="Value"/> es el texto canónico del valor en un catálogo tipado (C6),
/// y <see cref="TextRepeated"/> avisa que otra opción tiene el mismo texto, sin distinguir mayúsculas.
/// </summary>
public sealed record FormCatalogItemRow(Guid Id, string Code, string Text, string? Detail, string? Value, int Position, bool IsActive,
    bool TextRepeated);
