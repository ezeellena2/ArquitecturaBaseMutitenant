using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Una fila del listado. <see cref="OptionsCount"/>: en uno a mano, todas sus opciones; en uno de miembros, los que cumplen los
/// filtros; en uno de contactos o que no se puede usar, <c>null</c>.
/// </summary>
public sealed record FormCatalogRow(Guid Id, string Name, string? Description, FormCatalogSource Source, FormCatalogValueType ValueType,
    int? OptionsCount, bool IsActive, bool Usable, FormCatalogUnusableReason? UnusableReason);

/// <summary>Solo lo que dice el resumen del listado («7 catálogos · 6 activos», 262).</summary>
public sealed record FormCatalogFilterCountsResponse(int All, int Active);
