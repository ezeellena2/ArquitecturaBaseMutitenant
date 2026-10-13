using ArquitecturaBaseMultitenant.Api.Contracts.Common;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Contracts;

public sealed record WhatsAppPagedHttpQuery(int? Page, int? PageSize, string? Search);

public sealed record WhatsAppVersionHttpQuery(uint? Version);

/// <summary>Lo que devuelve el alta embebida de Meta: el código vive 30 segundos y nunca se registra.</summary>
public sealed record ConnectWhatsAppNumberHttpRequest([property: RawText] string? Code, [property: RawText] string? PhoneNumberId,
    [property: RawText] string? WabaId, Guid? CompanyId, bool? Coexistence)
{
    public override string ToString() => nameof(ConnectWhatsAppNumberHttpRequest);
}

public sealed record ConnectPlatformWhatsAppNumberHttpRequest([property: RawText] string? Code, [property: RawText] string? PhoneNumberId,
    [property: RawText] string? WabaId, bool? Coexistence)
{
    public override string ToString() => nameof(ConnectPlatformWhatsAppNumberHttpRequest);
}

public sealed record CheckWhatsAppNumberHttpRequest(uint? Version);

public sealed record UpdateWhatsAppNumberHttpRequest(Guid? CompanyId, uint? Version);

public sealed record UpdatePlatformWhatsAppNumberHttpRequest(bool? ConsultationsEnabled, uint? Version);

public sealed record UpdateWhatsAppBotHttpRequest(string? Welcome, string? OutOfHours, WhatsAppBotScheduleInput? Schedule,
    IReadOnlyList<WhatsAppBotMenuOptionInput>? Menu, bool? AskPromotions, bool? OfferAccountLink, Guid? EmployeesTeamId, uint? Version);

public sealed record UpdatePlatformWhatsAppBotHttpRequest(string? Welcome, string? OutOfHours, WhatsAppBotScheduleInput? Schedule,
    IReadOnlyList<WhatsAppBotMenuOptionInput>? Menu, uint? Version);

/// <summary>Plataforma no elige bandeja: sus equipos son de operadores.</summary>
public sealed record CreateWhatsAppTeamHttpRequest(string? Name, WhatsAppInbox? Inbox, IReadOnlyList<Guid>? MemberIds, int? NotifyAfterMinutes);

public sealed record UpdateWhatsAppTeamHttpRequest(string? Name, IReadOnlyList<Guid>? MemberIds, int? NotifyAfterMinutes, uint? Version);

public sealed record WhatsAppConversationListHttpQuery(int? Page, int? PageSize, string? Search, Guid? TeamId, WhatsAppConversationFilter? State);

public sealed record WhatsAppConversationMessagesHttpQuery(int? Page, int? PageSize);

public sealed record SendWhatsAppMessageHttpRequest(string? Text)
{
    public override string ToString() => nameof(SendWhatsAppMessageHttpRequest);
}

public sealed record AddWhatsAppNoteHttpRequest(string? Text)
{
    public override string ToString() => nameof(AddWhatsAppNoteHttpRequest);
}

public sealed record TransferWhatsAppConversationHttpRequest(Guid? TeamId, Guid? MemberId, uint? Version);

public sealed record SendWhatsAppTemplateHttpRequest(Guid? TemplateId, IReadOnlyList<string>? Variables)
{
    public override string ToString() => nameof(SendWhatsAppTemplateHttpRequest);
}

public sealed record WhatsAppContactListHttpQuery(int? Page, int? PageSize, string? Search, string? Tag, WhatsAppConsent? Consent);

public sealed record UpdateWhatsAppContactHttpRequest(string? Name, IReadOnlyList<string>? Tags, WhatsAppConsent? Consent, uint? Version)
{
    public override string ToString() => nameof(UpdateWhatsAppContactHttpRequest);
}

public sealed record ImportWhatsAppContactsHttpRequest(IFormFile? File, string? Tag)
{
    public override string ToString() => nameof(ImportWhatsAppContactsHttpRequest);
}

public sealed record WhatsAppTemplateListHttpQuery(int? Page, int? PageSize, string? Search, WhatsAppTemplateStatus? Status);

public sealed record PlatformWhatsAppTemplateListHttpQuery(int? Page, int? PageSize, string? Search, WhatsAppTemplateStatus? Status,
    WhatsAppTemplateGroup? Group);

public sealed record CreateWhatsAppTemplateHttpRequest(string? Name, WhatsAppTemplateCategory? Category, string? Language, string? Body,
    IReadOnlyList<WhatsAppTemplateVariableInput>? Variables);

public sealed record WhatsAppBroadcastDetailHttpQuery(int? Page, int? PageSize, WhatsAppRecipientStatus? Status);

public sealed record WhatsAppBroadcastEstimateHttpQuery(Guid? TemplateId, WhatsAppAudienceKind? Kind, string? Tag);

public sealed record CreateWhatsAppBroadcastHttpRequest(string? Name, Guid? TemplateId, IReadOnlyList<string>? Variables,
    WhatsAppAudienceInput? Audience, DateTime? ScheduledAtUtc);

public sealed record CancelWhatsAppBroadcastHttpRequest(uint? Version);

/// <summary>Cada teléfono con el país elegido; el back lo interpreta y no revela si tiene cuenta.</summary>
public sealed record WhatsAppPhoneImportHttpRequest(IReadOnlyList<PhoneInputHttpRequest>? Phones, Guid? CompanyId, IReadOnlyList<Guid>? RoleIds)
{
    public override string ToString() => nameof(WhatsAppPhoneImportHttpRequest);
}
