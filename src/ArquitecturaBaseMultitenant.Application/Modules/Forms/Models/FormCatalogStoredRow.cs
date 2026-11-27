using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Lo que la base sabe de un catálogo, sin lo que se calcula al leer: el servicio arma con esto la fila del listado y el detalle,
/// con el estado «No se puede usar» (<see cref="FormCatalog.UnusableReason(FormCatalogState, int, int, bool, bool, bool)"/>) y los nombres que dan las fuentes. Las cuentas son
/// las opciones de uno a mano, todas y las activas; en uno de miembros o de contactos, cero.
/// </summary>
public sealed record FormCatalogStoredRow(Guid Id, string Name, string? Description, FormCatalogSource Source,
    FormCatalogValueType ValueType, Domain.Forms.FormCatalogFilters? Filters, bool IsActive, int TotalItems, int ActiveItems,
    DateTime ModifiedAtUtc, Guid? ModifiedBy, uint Version);
