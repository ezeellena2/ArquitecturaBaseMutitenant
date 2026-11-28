using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;

/// <summary>
/// Prepara los cambios de los catálogos y sus opciones dentro de la transacción del servicio llamador. Cambiar las opciones de un
/// catálogo va bajo su bloqueo (C4): <see cref="LockAsync"/> lo toma antes de leerlo, y lo demás se lee después.
/// </summary>
public interface IFormCatalogRepository
{
    /// <summary>
    /// Toma el bloqueo de cada catálogo (advisory, en orden) dentro de la transacción del llamador; va antes de leer el catálogo y
    /// sus opciones.
    /// </summary>
    Task LockAsync(IReadOnlyCollection<Guid> catalogIds, CancellationToken ct);

    /// <summary>
    /// Toma, en una sola llamada a <see cref="LockAsync"/> y por eso siempre en el mismo orden, el bloqueo del catálogo, el de las
    /// fuentes de sus campos activos guardados y el de <paramref name="requestedSourceIds"/>, las que nombra el pedido (E13): dos
    /// catálogos que son fuente uno del otro no se esperan en cruz. Las fuentes guardadas se leen antes del bloqueo; si cambiaron
    /// en el medio, también cambió la versión del catálogo, y el servicio la compara después (409).
    /// </summary>
    Task LockWithSourcesAsync(Guid catalogId, IReadOnlyCollection<Guid> requestedSourceIds, CancellationToken ct);

    /// <summary>
    /// El catálogo, seguido para escribir; compara la versión al confirmar (409). Se lee después de <see cref="LockAsync"/>, y el
    /// servicio compara la versión antes de sus reglas, así una versión vieja da siempre el 409 común.
    /// </summary>
    Task<FormCatalog?> GetByIdAsync(Guid catalogId, uint expectedVersion, CancellationToken ct);

    /// <summary>Las opciones pedidas del catálogo, seguidas; una de otro catálogo no viene.</summary>
    Task<IReadOnlyList<FormCatalogItem>> ListItemsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct);

    /// <summary>
    /// Las opciones del catálogo que tienen guardado alguno de esos códigos normalizados, seguidas: el código único se comprueba
    /// bajo el bloqueo sin traer las 10.000 (C4).
    /// </summary>
    Task<IReadOnlyList<FormCatalogItem>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
        CancellationToken ct);

    /// <summary>
    /// Las opciones activas del catálogo cuyo valor del campo <paramref name="key"/>, sin espacios en los extremos y en mayúsculas,
    /// es alguno de <paramref name="upperValues"/>, seguidas: un valor único se comprueba bajo el bloqueo sin traer las 10.000 (E13).
    /// </summary>
    Task<IReadOnlyList<FormCatalogItem>> ListActiveItemsByFieldValuesAsync(Guid catalogId, string key,
        IReadOnlyCollection<string> upperValues, CancellationToken ct);

    /// <summary>Todas las opciones del catálogo por posición, seguidas: para reordenar o cargar de una vez (hasta 10.000).</summary>
    Task<IReadOnlyList<FormCatalogItem>> ListAllItemsAsync(Guid catalogId, CancellationToken ct);

    /// <summary>Las opciones del catálogo, activas y dadas de baja: el tope de 10.000 se comprueba bajo el bloqueo.</summary>
    Task<int> CountItemsAsync(Guid catalogId, CancellationToken ct);

    /// <summary>La última posición del catálogo, o <c>null</c> sin opciones.</summary>
    Task<int?> FindLastPositionAsync(Guid catalogId, CancellationToken ct);

    void Add(FormCatalog catalog);

    void Add(FormCatalogItem item);

    /// <summary>Toca la fila del catálogo, así su versión sube con cualquier cambio de sus opciones (C3).</summary>
    void MarkUpdated(FormCatalog catalog);

    void Remove(FormCatalogItem item);
}
