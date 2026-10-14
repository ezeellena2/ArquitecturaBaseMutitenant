using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>
/// Lee las bandejas: Clientes o Internos de la organización del claim (inbox) o las consultas de Plataforma (inbox null).
/// El estado de un mensaje que salió se deriva del outbox y de lo que informó Meta, sin escribir en otra organización.
/// </summary>
public interface IWhatsAppConversationReader
{
    /// <summary>Las más recientes primero; search busca en el nombre del contacto y en el número.</summary>
    Task<PagedResult<WhatsAppConversationRow>> ListAsync(WhatsAppInbox? inbox, WhatsAppConversationListRequest request, Guid userId,
        CancellationToken ct);
    Task<WhatsAppConversationRow?> FindAsync(WhatsAppInbox? inbox, Guid id, CancellationToken ct);
    /// <summary>La bandeja entera, o solo las que nadie tomó.</summary>
    Task<int> CountAsync(WhatsAppInbox? inbox, bool untaken, CancellationToken ct);
    /// <summary>La página 1 trae los más recientes, en orden descendente.</summary>
    Task<PagedResult<WhatsAppChatMessageResponse>> ListMessagesAsync(WhatsAppInbox? inbox, Guid chatId, PagedRequest request, CancellationToken ct);
    /// <summary>Cuántas conversaciones de la bandeja tiene tomadas cada cuenta.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountTakenAsync(WhatsAppInbox? inbox, IReadOnlyCollection<Guid> userIds, CancellationToken ct);
    /// <summary>Por equipo, las conversaciones que esperan que alguien las tome y desde cuándo espera la más vieja.</summary>
    Task<IReadOnlyList<WhatsAppWaitingRow>> ListWaitingAsync(bool platform, CancellationToken ct);
}

public sealed record WhatsAppWaitingRow(Guid TeamId, int Count, DateTime OldestWaitingSinceUtc);

/// <summary>
/// Una conversación con lo que la bandeja muestra: el contacto (Clientes), el miembro (Internos), el equipo, quien la tomó
/// (cuenta y, en la organización, su miembro) y el número que la recibió.
/// </summary>
public sealed record WhatsAppConversationRow(Guid Id, WhatsAppInbox? Inbox, Guid? ContactId, string? ContactName, IReadOnlyList<string> Tags,
    Guid? MemberUserId, string WaId, string? LastMessagePreview, DateTime LastMessageAtUtc, bool AwaitingReply, Guid? TeamId, string? TeamName,
    Guid? AssigneeUserId, Guid? AssigneeMemberId, DateTime? LastInboundAtUtc, Guid ChannelId, string? ChannelNumber,
    WhatsAppNumberStatus ChannelStatus, uint Version, bool LinkedToAccount = false, WhatsAppConsent? ContactConsent = null, string? ChannelWabaId = null)
{
    public override string ToString() => nameof(WhatsAppConversationRow);
}
