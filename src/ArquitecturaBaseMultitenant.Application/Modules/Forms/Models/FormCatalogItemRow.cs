using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Una opción de un catálogo a mano, por posición. <see cref="Value"/> es el texto canónico del valor en un catálogo tipado (C6),
/// y <see cref="TextRepeated"/> avisa que otra opción tiene el mismo texto, sin distinguir mayúsculas. Desde la E13,
/// <see cref="Fields"/> lleva el valor canónico de cada campo con valor, por la identidad del campo (un número como texto, una fecha
/// <c>yyyy-MM-dd</c>, sí/no como booleano y una fuente como la identidad de su opción), y <see cref="PresentationIssue"/> lo que no
/// entra en WhatsApp.
/// </summary>
public sealed record FormCatalogItemRow(Guid Id, string Code, string Text, string? Detail, string? Value, int Position, bool IsActive,
    bool TextRepeated, IReadOnlyDictionary<Guid, JsonElement> Fields, FormCatalogPresentationIssue? PresentationIssue)
{
    /// <summary>Una opción sin valores de campos.</summary>
    public static IReadOnlyDictionary<Guid, JsonElement> NoFields { get; } = new Dictionary<Guid, JsonElement>();
}
