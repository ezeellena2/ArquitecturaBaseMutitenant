using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

public enum WhatsAppConversationFilter { Untaken, Mine, All }

/// <summary>Un equipo: a quiénes les llegan los chats de una bandeja y cuándo se avisa por correo (null: no avisa).</summary>
public sealed record WhatsAppTeamResponse(Guid Id, string Name, WhatsAppInbox Inbox, int MemberCount, int? NotifyAfterMinutes, uint Version);

public sealed record WhatsAppTeamDetailResponse(Guid Id, string Name, WhatsAppInbox Inbox, PagedResult<WhatsAppReferenceResponse> Members,
    int? NotifyAfterMinutes, uint Version);

public sealed record WhatsAppTeamListRequest : PagedRequest;

public sealed record CreateWhatsAppTeamRequest(string? Name, WhatsAppInbox? Inbox, IReadOnlyList<Guid>? MemberIds, int? NotifyAfterMinutes);

public sealed record UpdateWhatsAppTeamRequest(string? Name, IReadOnlyList<Guid>? MemberIds, int? NotifyAfterMinutes, uint? Version);

public sealed record DeleteWhatsAppTeamRequest(uint? Version);

/// <summary>Una fila de la bandeja. El teléfono es el del cliente de la organización; la ventana de 24 h se mide desde su último mensaje.</summary>
public sealed record WhatsAppConversationRowResponse(Guid Id, WhatsAppInbox? Inbox, string? ContactName, string DisplayNumber,
    string? LastMessagePreview, DateTime LastMessageAtUtc, bool Unread, WhatsAppReferenceResponse? Team, WhatsAppReferenceResponse? Assignee,
    bool LinkedToAccount, DateTime? WindowOpenUntilUtc, bool ReadOnly, Guid? AccountId, uint Version);

public sealed record WhatsAppConversationCountsResponse(int Customers, int Internal, int Untaken);

public sealed record WhatsAppConversationListResponse(PagedResult<WhatsAppConversationRowResponse> Conversations, WhatsAppConversationCountsResponse Counts);

public sealed record WhatsAppConversationListRequest : PagedRequest
{
    public WhatsAppInbox? Inbox { get; init; }
    public Guid? TeamId { get; init; }
    public WhatsAppConversationFilter? State { get; init; }
}

/// <summary>Un mensaje de la conversación. Las notas nunca salen a Meta; los archivos se informan sin descarga hasta la E14.</summary>
public sealed record WhatsAppChatMessageResponse(Guid Id, WhatsAppChatMessageKind Kind, Domain.WhatsApp.WhatsAppMessageDirection Direction,
    string? Author, string? Text, WhatsAppMediaKind? MediaKind, WhatsAppChatMessageStatus Status, string? FailureReason, DateTime CreatedAtUtc);

public sealed record WhatsAppConversationResponse(WhatsAppConversationRowResponse Conversation, PagedResult<WhatsAppChatMessageResponse> Messages);

public sealed record WhatsAppConversationMessagesRequest : PagedRequest;

public sealed record SendWhatsAppMessageRequest(string? Text)
{
    public override string ToString() => nameof(SendWhatsAppMessageRequest);
}

public sealed record AddWhatsAppNoteRequest(string? Text)
{
    public override string ToString() => nameof(AddWhatsAppNoteRequest);
}

public sealed record TransferWhatsAppConversationRequest(Guid? TeamId, Guid? MemberId, uint? Version);

public sealed record WhatsAppTemplateOptionResponse(Guid TemplateId, string Name, WhatsAppTemplateCategory Category, IReadOnlyList<string> Variables,
    Money EstimatedCost);

public sealed record WhatsAppTemplateOptionListRequest : PagedRequest;

public sealed record SendWhatsAppTemplateRequest(Guid? TemplateId, IReadOnlyList<string>? Variables)
{
    public override string ToString() => nameof(SendWhatsAppTemplateRequest);
}
