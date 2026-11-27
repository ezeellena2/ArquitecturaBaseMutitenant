using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;

/// <summary>
/// La barrera de las operaciones de archivo de los catálogos (C11), en PostgreSQL y entre todas las copias: una por persona en la
/// organización activa, a lo sumo <c>Forms:CatalogFiles:MaxPerOrganization</c> a la vez en una organización y
/// <c>MaxPerInstallation</c> en toda la instalación. Se toma sin esperar a otra operación y fuera de toda transacción del servicio,
/// se suelta al descartar lo que devuelve y, si el proceso se cae, vence sola a los dos minutos.
/// </summary>
public interface IFormWorkLease
{
    /// <summary>
    /// Toma la barrera de la persona en la organización activa. Falla con <see cref="FormCatalogImportErrors.Busy"/> si ya tiene una
    /// vigente, y con <see cref="FormCatalogImportErrors.TooManyAtOnce"/> si su organización o la instalación no tienen lugar.
    /// </summary>
    Task<Result<IAsyncDisposable>> TryTakeAsync(Guid userId, FormWorkLeaseKind kind, CancellationToken ct);
}
