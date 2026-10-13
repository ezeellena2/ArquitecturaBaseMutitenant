using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

/// <summary>Un contacto de la organización con su consentimiento de promociones y el origen de ese dato.</summary>
public sealed record WhatsAppContactResponse(Guid Id, string? Name, string DisplayNumber, IReadOnlyList<string> Tags, WhatsAppConsent Consent,
    WhatsAppConsentSource? ConsentSource, DateTime? ConsentChangedAtUtc, bool LinkedToAccount, DateTime? LastConversationAtUtc, uint Version);

public sealed record WhatsAppContactListResponse(PagedResult<WhatsAppContactResponse> Contacts, int Total, int Accepting);

public sealed record WhatsAppContactListRequest : PagedRequest
{
    public string? Tag { get; init; }
    public WhatsAppConsent? Consent { get; init; }
}

public sealed record WhatsAppContactConversationResponse(Guid ConversationId, DateTime StartedAtUtc, WhatsAppReferenceResponse? Team,
    WhatsAppReferenceResponse? Assignee, int MessageCount);

public sealed record WhatsAppContactBroadcastResponse(Guid BroadcastId, string Name, DateTime SentAtUtc, WhatsAppRecipientStatus Status);

public sealed record WhatsAppContactChildListRequest : PagedRequest;

public sealed record UpdateWhatsAppContactRequest(string? Name, IReadOnlyList<string>? Tags, WhatsAppConsent? Consent, uint? Version)
{
    public override string ToString() => nameof(UpdateWhatsAppContactRequest);
}

public sealed record WhatsAppTagListRequest : PagedRequest;

/// <summary>Importa contactos de un .xlsx o .csv como el ejemplo; sin «Sí» queda «Sin respuesta».</summary>
public sealed record ImportWhatsAppContactsRequest(string? FileName, string? ContentType, Stream? Content, string? Tag)
{
    public override string ToString() => nameof(ImportWhatsAppContactsRequest);
}

public sealed record WhatsAppImportRejectionResponse(int Row, string Reason);

public sealed record WhatsAppImportResponse(int Imported, int Updated, PagedResult<WhatsAppImportRejectionResponse> Rejected);

/// <summary>Lo que haría la importación, sin guardar: filas nuevas, actualizadas y rechazadas.</summary>
public sealed record WhatsAppImportPreviewResponse(int Rows, int New, int Updated, PagedResult<WhatsAppImportRejectionResponse> Rejected);

public sealed record WhatsAppTemplateResponse(Guid Id, string Name, WhatsAppTemplateCategory Category, string Language, string Body,
    IReadOnlyList<string> Variables, WhatsAppTemplateStatus Status, string? RejectionReason, bool IsSystem, DateTime UpdatedAtUtc, uint Version);

public enum WhatsAppTemplateGroup { Consultations, System }

public sealed record WhatsAppTemplateListResponse(PagedResult<WhatsAppTemplateResponse> Templates, int Approved);

public sealed record WhatsAppTemplateListRequest : PagedRequest
{
    public WhatsAppTemplateStatus? Status { get; init; }
    public WhatsAppTemplateGroup? Group { get; init; }
}

public sealed record WhatsAppTemplateVariableInput(string? Label, string? Example);

public sealed record CreateWhatsAppTemplateRequest(string? Name, WhatsAppTemplateCategory? Category, string? Language, string? Body,
    IReadOnlyList<WhatsAppTemplateVariableInput>? Variables);

public sealed record DeleteWhatsAppTemplateRequest(uint? Version);

public sealed record WhatsAppAudienceInput(WhatsAppAudienceKind? Kind, string? Tag);

public sealed record WhatsAppAudienceResponse(WhatsAppAudienceKind Kind, string? Tag);

public sealed record WhatsAppBroadcastResponse(Guid Id, string Name, WhatsAppReferenceResponse Template, WhatsAppAudienceResponse Audience,
    int Recipients, WhatsAppBroadcastStatus Status, DateTime? ScheduledAtUtc, DateTime? SentAtUtc, int Delivered, int Read, int Replied,
    int Unsubscribed, int Failed, Money EstimatedCost, uint Version);

public sealed record WhatsAppBroadcastListRequest : PagedRequest;

public sealed record WhatsAppBroadcastListResponse(PagedResult<WhatsAppBroadcastResponse> Broadcasts, int Scheduled);

public sealed record WhatsAppRecipientResponse(Guid ContactId, string? ContactName, string DisplayNumber, WhatsAppRecipientStatus Status,
    string? FailureReason, DateTime ChangedAtUtc);

public sealed record WhatsAppBroadcastDetailResponse(WhatsAppBroadcastResponse Broadcast, PagedResult<WhatsAppRecipientResponse> Recipients);

public sealed record WhatsAppBroadcastDetailRequest : PagedRequest
{
    public WhatsAppRecipientStatus? Status { get; init; }
}

public sealed record WhatsAppBroadcastEstimateRequest(Guid? TemplateId, WhatsAppAudienceKind? Kind, string? Tag);

public sealed record WhatsAppBroadcastEstimateResponse(int Recipients, Money EstimatedCost);

public sealed record CreateWhatsAppBroadcastRequest(string? Name, Guid? TemplateId, IReadOnlyList<string>? Variables, WhatsAppAudienceInput? Audience,
    DateTime? ScheduledAtUtc);

public sealed record CancelWhatsAppBroadcastRequest(uint? Version);

/// <summary>Teléfonos a cargar como empleados: cada uno con el país elegido, interpretado por el back.</summary>
public sealed record WhatsAppPhoneImportRequest(IReadOnlyList<PhoneInputRequest>? Phones, Guid? CompanyId, IReadOnlyList<Guid>? RoleIds)
{
    public override string ToString() => nameof(WhatsAppPhoneImportRequest);
}

public sealed record WhatsAppPhoneImportRowResponse(int Row, string? Phone, string? MemberName, WhatsAppPhoneImportOutcome Outcome);

public sealed record WhatsAppPhoneImportPreviewResponse(IReadOnlyList<WhatsAppPhoneImportRowResponse> Rows, int AlreadyMembers, int Invites, int Invalid);

public sealed record WhatsAppPhoneImportResponse(int Linked, int Invited, int Invalid);

/// <summary>Una conversación vinculada, como la ve la persona: solo desde que la vinculó (D5).</summary>
public sealed record PersonalWhatsAppConversationResponse(Guid Id, string OrganizationName, DateTime LinkedAtUtc, DateTime? LastMessageAtUtc,
    string? LastMessagePreview, string DisplayNumber);

public sealed record PersonalWhatsAppConversationListRequest : PagedRequest;

public sealed record PersonalWhatsAppConversationDetailResponse(PersonalWhatsAppConversationResponse Conversation,
    PagedResult<WhatsAppChatMessageResponse> Messages);

public sealed record WhatsAppFileResponse(Stream Content, string MediaType, string FileName)
{
    public override string ToString() => "WhatsAppFileResponse { [REDACTED] }";
}
