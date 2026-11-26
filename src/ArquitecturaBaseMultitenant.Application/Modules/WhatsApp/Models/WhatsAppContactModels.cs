using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

/// <summary>
/// Un contacto de la organización con su consentimiento de promociones y el origen de ese dato. displayNumber va en E.164 (el
/// front lo formatea); latestConversationId es la conversación más reciente, para «Ver chat».
/// </summary>
public sealed record WhatsAppContactResponse(Guid Id, string? Name, string DisplayNumber, IReadOnlyList<string> Tags, WhatsAppConsent Consent,
    WhatsAppConsentSource? ConsentSource, DateTime? ConsentChangedAtUtc, bool LinkedToAccount, DateTime? LastConversationAtUtc, uint Version,
    Guid? LatestConversationId);

public sealed record WhatsAppContactListResponse(PagedResult<WhatsAppContactResponse> Contacts, int Total, int Accepting);

public sealed record WhatsAppContactListRequest : PagedRequest
{
    /// <summary>Los ids de las columnas con un dato (264); las etiquetas son varias por contacto y no ordenan.</summary>
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "number", "consent", "lastConversationAtUtc"];

    public string? Tag { get; init; }
    public WhatsAppConsent? Consent { get; init; }
}

public sealed record WhatsAppContactConversationResponse(Guid ConversationId, DateTime StartedAtUtc, WhatsAppReferenceResponse? Team,
    WhatsAppReferenceResponse? Assignee, int MessageCount, string? LastMessagePreview);

public sealed record WhatsAppContactBroadcastResponse(Guid BroadcastId, string Name, DateTime SentAtUtc, WhatsAppRecipientStatus Status);

/// <summary>
/// Las conversaciones de la ficha de un contacto, por las columnas de su tabla (264); sin orden, la más nueva primero. El último
/// mensaje es contenido cifrado del chat, que ningún lector ordena, y quien la atiende es un nombre de la cuenta, que el módulo
/// no lee: esas dos columnas no ordenan.
/// </summary>
public sealed record WhatsAppContactConversationListRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["startedAtUtc", "team", "messageCount"];
}

/// <summary>Las difusiones que le salieron a un contacto, por las columnas de su tabla (264); el estado, por su circuito.</summary>
public sealed record WhatsAppContactBroadcastListRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "sentAtUtc", "status"];
}

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

/// <summary>
/// Una plantilla de Meta. languages son sus idiomas, el principal primero (hasta 10; las del sistema traen es y en), cada uno con
/// su categoría; category es la del primero. variables trae el nombre y el ejemplo de cada una, el mismo par del alta. title es
/// el nombre visible de las del sistema y null en las demás.
/// </summary>
public sealed record WhatsAppTemplateResponse(Guid Id, string Name, WhatsAppTemplateCategory Category, IReadOnlyList<WhatsAppTemplateLanguageResponse> Languages,
    string Body, IReadOnlyList<WhatsAppTemplateVariableResponse> Variables, WhatsAppTemplateStatus Status, string? RejectionReason, bool IsSystem,
    DateTime UpdatedAtUtc, uint Version, string? Title);

/// <summary>Un idioma de la plantilla: culture es un código del catálogo de culturas (es-AR), nunca el de Meta (es_AR).</summary>
public sealed record WhatsAppTemplateLanguageResponse(string Culture, WhatsAppTemplateCategory Category);

public sealed record WhatsAppTemplateVariableResponse(string Label, string Example);

public enum WhatsAppTemplateGroup { Consultations, System }

/// <summary>La lista con las aprobadas y, en Plataforma, cuántas hay por grupo (consultations y system); en la organización van en null.</summary>
public sealed record WhatsAppTemplateListResponse(PagedResult<WhatsAppTemplateResponse> Templates, int Approved, int? Consultations, int? System);

public sealed record WhatsAppTemplateListRequest : PagedRequest
{
    /// <summary>Los ids de las columnas con un dato (264); los idiomas son varios por plantilla y no ordenan.</summary>
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "category", "status", "updatedAtUtc"];

    public WhatsAppTemplateStatus? Status { get; init; }
    public WhatsAppTemplateGroup? Group { get; init; }
}

public sealed record WhatsAppTemplateVariableInput(string? Label, string? Example);

/// <summary>language es un código de cultura del catálogo (es-AR); el back lo traduce al de Meta (es_AR).</summary>
public sealed record CreateWhatsAppTemplateRequest(string? Name, WhatsAppTemplateCategory? Category, string? Language, string? Body,
    IReadOnlyList<WhatsAppTemplateVariableInput>? Variables);

public sealed record DeleteWhatsAppTemplateRequest(uint? Version);

public sealed record WhatsAppAudienceInput(WhatsAppAudienceKind? Kind, string? Tag);

public sealed record WhatsAppAudienceResponse(WhatsAppAudienceKind Kind, string? Tag);

/// <summary>
/// variables son los valores con que se creó la difusión, para duplicarla. uncertain cuenta los destinatarios «Sin confirmar»
/// (decisión 255): la cola no sabe si salieron, y no cuentan en delivered ni en failed.
/// </summary>
public sealed record WhatsAppBroadcastResponse(Guid Id, string Name, WhatsAppReferenceResponse Template, IReadOnlyList<string> Variables, WhatsAppAudienceResponse Audience,
    int Recipients, WhatsAppBroadcastStatus Status, DateTime? ScheduledAtUtc, DateTime? SentAtUtc, int Delivered, int Read, int Replied,
    int Unsubscribed, int Failed, int Uncertain, Money EstimatedCost, uint Version);

public sealed record WhatsAppBroadcastListRequest : PagedRequest
{
    /// <summary>Los ids de las columnas con un dato (264); date es la fecha de la fila (cuándo salió o para cuándo está).</summary>
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "template", "recipients", "status", "delivered", "read", "date"];

    public WhatsAppBroadcastStatus? Status { get; init; }
}

public sealed record WhatsAppBroadcastListResponse(PagedResult<WhatsAppBroadcastResponse> Broadcasts, int Scheduled);

public sealed record WhatsAppRecipientResponse(Guid ContactId, string? ContactName, string DisplayNumber, WhatsAppRecipientStatus Status,
    string? FailureReason, DateTime ChangedAtUtc);

public sealed record WhatsAppBroadcastDetailResponse(WhatsAppBroadcastResponse Broadcast, PagedResult<WhatsAppRecipientResponse> Recipients);

public sealed record WhatsAppBroadcastDetailRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["contact", "number", "status", "changedAtUtc"];

    public WhatsAppRecipientStatus? Status { get; init; }
}

public sealed record WhatsAppBroadcastEstimateRequest(Guid? TemplateId, WhatsAppAudienceKind? Kind, string? Tag);

/// <summary>Cuántos reciben y cuánto cuesta, con el país (ISO 3166) de las tarifas usadas.</summary>
public sealed record WhatsAppBroadcastEstimateResponse(int Recipients, Money EstimatedCost, string Country);

public sealed record CreateWhatsAppBroadcastRequest(string? Name, Guid? TemplateId, IReadOnlyList<string>? Variables, WhatsAppAudienceInput? Audience,
    DateTime? ScheduledAtUtc);

public sealed record CancelWhatsAppBroadcastRequest(uint? Version);

/// <summary>Teléfonos a cargar como empleados: cada uno con el país elegido, interpretado por el back.</summary>
/// <summary>«Cargar teléfonos»; <c>ConsentConfirmed</c> es la casilla de la importación (179) y la vista previa no la pide.</summary>
public sealed record WhatsAppPhoneImportRequest(IReadOnlyList<PhoneInputRequest>? Phones, Guid? CompanyId, IReadOnlyList<Guid>? RoleIds,
    bool? ConsentConfirmed = null)
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
