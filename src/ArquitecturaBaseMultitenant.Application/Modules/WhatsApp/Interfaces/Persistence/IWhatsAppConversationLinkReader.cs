using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>«Mensajes» de la persona: sus conversaciones vinculadas y lo que se copió desde que las vinculó (D5).</summary>
public interface IWhatsAppConversationLinkReader
{
    /// <summary>La de actividad más reciente primero.</summary>
    Task<PagedResult<WhatsAppConversationLinkRow>> ListAsync(PagedRequest request, CancellationToken ct);
    Task<WhatsAppConversationLinkRow?> FindAsync(Guid id, CancellationToken ct);
    /// <summary>La página 1 trae los más recientes, en orden descendente.</summary>
    Task<PagedResult<WhatsAppChatMessageResponse>> ListMessagesAsync(Guid linkId, PagedRequest request, CancellationToken ct);
}

/// <summary>Una conversación vinculada con la organización (su espacio) y lo que la persona ve de ella.</summary>
public sealed record WhatsAppConversationLinkRow(Guid Id, Guid OrganizationId, string OrganizationName, DateTime LinkedAtUtc, DateTime? LastMessageAtUtc,
    string? LastMessagePreview, string DisplayNumber)
{
    public PersonalWhatsAppConversationResponse Response() =>
        new(Id, OrganizationName, LinkedAtUtc, LastMessageAtUtc, LastMessagePreview, DisplayNumber);
}
