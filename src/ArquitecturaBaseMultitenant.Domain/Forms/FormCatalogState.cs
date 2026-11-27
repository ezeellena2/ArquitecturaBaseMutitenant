namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Lo que pide la regla de «No se puede usar» de un catálogo (<see cref="FormCatalog.UnusableReason(FormCatalogState, int, int, bool, bool, bool)"/>):
/// su origen, si está activo y sus filtros, leídos de la base sin la entidad.
/// </summary>
public sealed record FormCatalogState(FormCatalogSource Source, bool IsActive, FormCatalogFilters? Filters);
