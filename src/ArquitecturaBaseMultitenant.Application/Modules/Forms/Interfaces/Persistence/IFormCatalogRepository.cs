using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;

/// <summary>
/// Prepara los cambios de los catálogos y sus opciones dentro de la transacción del servicio llamador. Cambiar las opciones de un
/// catálogo va bajo su bloqueo (C4): <see cref="GetByIdAsync"/> lo toma antes de leerlo, y lo demás se lee después.
/// </summary>
public interface IFormCatalogRepository
{
    /// <summary>El catálogo, seguido para escribir, después de tomar su bloqueo; compara la versión al confirmar (409).</summary>
    Task<FormCatalog?> GetByIdAsync(Guid catalogId, uint expectedVersion, CancellationToken ct);

    /// <summary>Las opciones pedidas del catálogo, seguidas; una de otro catálogo no viene.</summary>
    Task<IReadOnlyList<FormCatalogItem>> ListItemsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct);

    /// <summary>
    /// Las opciones del catálogo que tienen guardado alguno de esos códigos normalizados, seguidas: el código único se comprueba
    /// bajo el bloqueo sin traer las 10.000 (C4).
    /// </summary>
    Task<IReadOnlyList<FormCatalogItem>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
        CancellationToken ct);

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
