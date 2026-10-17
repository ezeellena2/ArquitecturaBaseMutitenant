using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

public enum WhatsAppConversationFilter { Untaken, Mine, All }

/// <summary>
/// Un equipo: a quiénes les llegan los chats de una bandeja y cuándo se avisa por correo (null: no avisa). La vista previa
/// trae hasta tres nombres de integrantes.
/// </summary>
public sealed record WhatsAppTeamResponse(Guid Id, string Name, WhatsAppInbox Inbox, int MemberCount, IReadOnlyList<string> MemberPreview,
    int? NotifyAfterMinutes, uint Version);

/// <summary>
/// El equipo con todos sus integrantes, sin paginar (hasta 200): el PUT reemplaza la lista entera. En la organización cada
/// integrante es un miembro (memberId); en Plataforma, un operador (accountId).
/// </summary>
public sealed record WhatsAppTeamDetailResponse(Guid Id, string Name, WhatsAppInbox Inbox, IReadOnlyList<WhatsAppReferenceResponse> Members,
    int? NotifyAfterMinutes, uint Version);

public sealed record WhatsAppTeamListRequest : PagedRequest;

/// <summary>Las personas que se pueden sumar a un equipo: en la organización, miembros activos (memberId); en Plataforma, operadores (accountId).</summary>
public sealed record WhatsAppTeamMemberOptionListRequest : PagedRequest;

public sealed record CreateWhatsAppTeamRequest(string? Name, WhatsAppInbox? Inbox, IReadOnlyList<Guid>? MemberIds, int? NotifyAfterMinutes);

public sealed record UpdateWhatsAppTeamRequest(string? Name, IReadOnlyList<Guid>? MemberIds, int? NotifyAfterMinutes, uint? Version);

public sealed record DeleteWhatsAppTeamRequest(uint? Version);

/// <summary>
/// Una fila de la bandeja. displayNumber es el teléfono de la otra parte en E.164 (el front lo formatea); channel es el número
/// que la recibió, con su estado; la ventana de 24 h se mide desde el último mensaje de la otra parte. assignedToMe lo
/// calcula el back con la sesión.
/// </summary>
public sealed record WhatsAppConversationRowResponse(Guid Id, WhatsAppInbox? Inbox, Guid? ContactId, string? ContactName, string DisplayNumber,
    IReadOnlyList<string> Tags, string? LastMessagePreview, DateTime LastMessageAtUtc, bool Unread, WhatsAppReferenceResponse? Team,
    WhatsAppReferenceResponse? Assignee, bool LinkedToAccount, DateTime? WindowOpenUntilUtc, bool ReadOnly, Guid? AccountId, uint Version,
    WhatsAppConversationChannelResponse Channel, bool AssignedToMe);

/// <summary>El número que recibió la conversación y su estado (Connected, PaymentMissing o Disconnected); displayNumber en E.164.</summary>
public sealed record WhatsAppConversationChannelResponse(Guid Id, string DisplayNumber, WhatsAppNumberStatus Status);

/// <summary>
/// Cuentan la bandeja entera y no dependen de search, state ni teamId (conversations.totalCount es solo para paginar).
/// Organización: customers e internal son el total de cada bandeja, con 0 en la que la persona no puede ver; untaken es de la
/// bandeja de la ruta. Plataforma: customers son todas las consultas, internal es 0 y untaken, las consultas sin tomar.
/// </summary>
public sealed record WhatsAppConversationCountsResponse(int Customers, int Internal, int Untaken);

public sealed record WhatsAppConversationListResponse(PagedResult<WhatsAppConversationRowResponse> Conversations, WhatsAppConversationCountsResponse Counts);

public sealed record WhatsAppConversationListRequest : PagedRequest
{
    public WhatsAppInbox? Inbox { get; init; }
    public Guid? TeamId { get; init; }
    public WhatsAppConversationFilter? State { get; init; }
}

/// <summary>
/// Un mensaje de la conversación. Las notas nunca salen a Meta; los archivos se informan sin descarga hasta la E14. Un
/// mensaje saliente sin author es la respuesta automática del bot.
/// </summary>
public sealed record WhatsAppChatMessageResponse(Guid Id, WhatsAppChatMessageKind Kind, Domain.WhatsApp.WhatsAppMessageDirection Direction,
    string? Author, string? Text, WhatsAppMediaKind? MediaKind, WhatsAppChatMessageStatus Status, string? FailureReason, DateTime CreatedAtUtc);

/// <summary>La conversación y sus mensajes: la página 1 trae los más recientes, en orden descendente.</summary>
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

public sealed record WhatsAppTemplateOptionResponse(Guid TemplateId, string Name, WhatsAppTemplateCategory Category, string Body,
    IReadOnlyList<string> Variables, Money EstimatedCost);

/// <summary>Un equipo al que se puede pasar el chat, con la cantidad de personas.</summary>
public sealed record WhatsAppTransferTeamResponse(Guid Id, string Name, int MemberCount);

/// <summary>Una persona del equipo del chat a la que se le puede pasar, con los chats que ya tiene tomados.</summary>
public sealed record WhatsAppTransferPersonResponse(Guid Id, string Name, int TakenCount);

public sealed record WhatsAppTransferOptionListRequest : PagedRequest;

/// <summary>Los sin tomar de cada bandeja que la persona puede ver; null en la que no tiene permiso.</summary>
public sealed record WhatsAppUntakenResponse(int? Customers, int? Internal);

public sealed record WhatsAppTemplateOptionListRequest : PagedRequest;

/// <summary>Los equipos de la bandeja de la ruta, para el filtro «Equipo».</summary>
public sealed record WhatsAppInboxTeamListRequest : PagedRequest;

public sealed record SendWhatsAppTemplateRequest(Guid? TemplateId, IReadOnlyList<string>? Variables)
{
    public override string ToString() => nameof(SendWhatsAppTemplateRequest);
}
