using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Los campos de un catálogo como los guarda la base (E13): el jsonb de los campos, también los quitados, el de la presentación y
/// cuántas opciones tienen un valor de cada clave, activas y dadas de baja, contadas con un solo <c>GROUP BY</c>.
/// </summary>
public sealed record FormCatalogFieldsRow(string FieldsJson, string Presentation, IReadOnlyDictionary<string, int> ValuesCount);

/// <summary>Un catálogo que puede ser la fuente de un campo «De un catálogo» (111): a mano y activo, de la misma organización.</summary>
public sealed record FormCatalogFieldSourceRow(Guid Id, string Name, FormCatalogSource Source, bool IsActive);
