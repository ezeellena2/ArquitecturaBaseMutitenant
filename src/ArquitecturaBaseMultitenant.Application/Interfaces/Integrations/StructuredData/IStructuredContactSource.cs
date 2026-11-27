using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.StructuredData;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.StructuredData;

/// <summary>
/// Los contactos de la organización del claim que da un canal de mensajería, para las listas que arma otra área sin conocer el
/// canal (C12 de la E12): Forms nunca referencia a WhatsApp. Sin el canal (módulo no instalado o apagado para la organización),
/// no hay contactos. Nunca sale un teléfono. La lista lee con un timeout corto (3 s).
/// </summary>
public interface IStructuredContactSource
{
    /// <summary>Si la organización tiene el canal; sin él, el origen no se ofrece.</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct);

    /// <summary>Los que cumplen las condiciones, por nombre, hasta el tope; cuenta el total solo si lo pasan.</summary>
    Task<StructuredContactPage> ListAsync(StructuredContactQuery query, CancellationToken ct);

    /// <summary>Un contacto con nombre de la organización; <c>null</c> si no está o no hay canal.</summary>
    Task<StructuredContact?> FindAsync(Guid contactId, CancellationToken ct);

    /// <summary>Los contactos con nombre, por nombre; la búsqueda mira el nombre.</summary>
    Task<PagedResult<StructuredContact>> ListPageAsync(PagedRequest request, CancellationToken ct);

    /// <summary>Las etiquetas que usan los contactos, en orden alfabético, con cuántos la tienen.</summary>
    Task<PagedResult<StructuredContactTag>> ListTagsAsync(PagedRequest request, CancellationToken ct);
}
