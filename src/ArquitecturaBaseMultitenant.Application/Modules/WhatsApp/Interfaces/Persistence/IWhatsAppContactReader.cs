using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
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
    Task<PagedResult<WhatsAppContactConversationRow>> ListConversationsAsync(Guid contactId, PagedRequest request, CancellationToken ct);
    /// <summary>Las etiquetas en uso, en orden alfabético.</summary>
    Task<PagedResult<string>> ListTagsAsync(PagedRequest request, CancellationToken ct);
    /// <summary>Cuáles de esos números ya son contactos.</summary>
    Task<IReadOnlySet<string>> FindExistingAsync(IReadOnlyCollection<string> waIds, CancellationToken ct);
}

public sealed record WhatsAppContactCounts(int Total, int Accepting);

public sealed record WhatsAppContactConversationRow(Guid ConversationId, DateTime StartedAtUtc, Guid? TeamId, string? TeamName,
    Guid? AssigneeUserId, Guid? AssigneeMemberId, int MessageCount, string? LastMessagePreview);

/// <summary>Un contacto que recibe una difusión: su número en E.164.</summary>
public sealed record WhatsAppAudienceMember(Guid Id, string WaId);
