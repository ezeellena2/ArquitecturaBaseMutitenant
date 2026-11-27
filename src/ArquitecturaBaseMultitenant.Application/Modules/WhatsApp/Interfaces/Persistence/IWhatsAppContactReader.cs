using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.StructuredData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Lee los contactos de la organización del claim, sus etiquetas y sus conversaciones.</summary>
public interface IWhatsAppContactReader
{
    /// <summary>Los contactos que aceptan promociones; con etiqueta, solo los que la tienen.</summary>
    Task<IReadOnlyList<WhatsAppAudienceMember>> ListAudienceAsync(string? tag, CancellationToken ct);
    /// <summary>El consentimiento actual de esos contactos.</summary>
    Task<IReadOnlyDictionary<Guid, WhatsAppConsent>> ListConsentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    /// <summary>Por nombre (los sin nombre al final); search busca en el nombre y el número.</summary>
    Task<PagedResult<WhatsAppContactResponse>> ListAsync(WhatsAppContactListRequest request, CancellationToken ct);
    Task<WhatsAppContactResponse?> FindAsync(Guid id, CancellationToken ct);
    /// <summary>Todos los contactos y los que aceptan promociones, sin filtros.</summary>
    Task<WhatsAppContactCounts> CountAsync(CancellationToken ct);
    /// <summary>Las conversaciones del contacto, la más reciente primero; quien la tomó, por su cuenta y su miembro.</summary>
    Task<PagedResult<WhatsAppContactConversationRow>> ListConversationsAsync(Guid contactId, WhatsAppContactConversationListRequest request, CancellationToken ct);
    /// <summary>Las etiquetas en uso, en orden alfabético.</summary>
    Task<PagedResult<string>> ListTagsAsync(PagedRequest request, CancellationToken ct);
    /// <summary>Cuáles de esos números ya son contactos.</summary>
    Task<IReadOnlySet<string>> ListExistingAsync(IReadOnlyCollection<string> waIds, CancellationToken ct);
    /// <summary>
    /// Los contactos con nombre para una lista de otra área (C12 de la E12), sin teléfono: por nombre, hasta el tope, con un
    /// timeout corto; cuenta el total solo si lo pasan.
    /// </summary>
    Task<StructuredContactPage> ListStructuredAsync(StructuredContactQuery query, CancellationToken ct);
    /// <summary>Un contacto con nombre, sin teléfono.</summary>
    Task<StructuredContact?> FindStructuredAsync(Guid id, CancellationToken ct);
    /// <summary>Los contactos con nombre, por nombre, sin teléfono; search busca solo en el nombre.</summary>
    Task<PagedResult<StructuredContact>> ListStructuredPageAsync(PagedRequest request, CancellationToken ct);
    /// <summary>Las etiquetas de los contactos con nombre, en orden alfabético, con cuántos la tienen.</summary>
    Task<PagedResult<StructuredContactTag>> ListTagCountsAsync(PagedRequest request, CancellationToken ct);
}

public sealed record WhatsAppContactCounts(int Total, int Accepting);

public sealed record WhatsAppContactConversationRow(Guid ConversationId, DateTime StartedAtUtc, Guid? TeamId, string? TeamName,
    Guid? AssigneeUserId, Guid? AssigneeMemberId, int MessageCount, string? LastMessagePreview);

/// <summary>Un contacto que recibe una difusión: su número en E.164.</summary>
public sealed record WhatsAppAudienceMember(Guid Id, string WaId);
