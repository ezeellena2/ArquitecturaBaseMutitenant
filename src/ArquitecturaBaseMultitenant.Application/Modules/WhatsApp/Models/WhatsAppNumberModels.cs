using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

/// <summary>Datos públicos del alta embebida de Meta: la app y la configuración de Facebook Login for Business.</summary>
public sealed record WhatsAppSignupConfigResponse(string AppId, string ConfigId, string SdkVersion);

public sealed record WhatsAppReferenceResponse(Guid Id, string Name);

/// <summary>Un número conectado. El cupo gratis es de mensajes de servicio entregados en el mes de la cuenta de Meta.</summary>
public sealed record WhatsAppNumberResponse(Guid Id, string DisplayNumber, string? VerifiedName, WhatsAppReferenceResponse? Company,
    WhatsAppNumberStatus Status, bool Coexistence, int ServiceMessagesThisMonth, int FreeServiceMessages, int ChargedServiceMessagesThisMonth,
    DateTime ConnectedAtUtc, uint Version);

/// <summary>La lista de números, cuántos no tienen medio de pago y los datos del alta.</summary>
public sealed record WhatsAppNumberListResponse(PagedResult<WhatsAppNumberResponse> Numbers, int PaymentMissing, WhatsAppSignupConfigResponse? Signup);

public sealed record WhatsAppNumberListRequest : PagedRequest;

/// <summary>Las empresas activas que puede atender un número («Atiende a»), con el permiso de administrar WhatsApp.</summary>
public sealed record WhatsAppCompanyOptionListRequest : PagedRequest;

/// <summary>
/// Resultado del alta embebida. El código de Meta vive 30 segundos y nunca se registra. En coexistencia el FINISH de Meta trae
/// solo la cuenta (wabaId): phoneNumberId es opcional y el back resuelve el número desde la cuenta.
/// </summary>
public sealed record ConnectWhatsAppNumberRequest(string? Code, string? PhoneNumberId, string? WabaId, Guid? CompanyId, bool? Coexistence)
{
    public override string ToString() => nameof(ConnectWhatsAppNumberRequest);
}

public sealed record CheckWhatsAppNumberRequest(uint? Version);

public sealed record UpdateWhatsAppNumberRequest(Guid? CompanyId, uint? Version);

public sealed record DeleteWhatsAppNumberRequest(uint? Version);

/// <summary>El número de Plataforma: sin número, con número o sin medio de pago. Códigos y avisos se cuentan aparte.</summary>
public sealed record PlatformWhatsAppNumberResponse(WhatsAppNumberResponse? Number, bool ConsultationsEnabled, int SystemMessagesThisMonth,
    WhatsAppSignupConfigResponse? Signup);

public sealed record ConnectPlatformWhatsAppNumberRequest(string? Code, string? PhoneNumberId, string? WabaId, bool? Coexistence)
{
    public override string ToString() => nameof(ConnectPlatformWhatsAppNumberRequest);
}

public sealed record UpdatePlatformWhatsAppNumberRequest(bool? ConsultationsEnabled, uint? Version);

public sealed record WhatsAppBotScheduleDay(DayOfWeek Day, TimeOnly From, TimeOnly To);

public sealed record WhatsAppBotSchedule(string TimeZoneId, IReadOnlyList<WhatsAppBotScheduleDay> Days);

/// <summary>Una opción del menú: pasa a un equipo o contesta un texto. La del sistema de Plataforma no se edita.</summary>
public sealed record WhatsAppBotMenuOption(Guid? Id, string Label, WhatsAppBotAction Action, Guid? TeamId, string? Reply, bool IsSystem);

public sealed record WhatsAppBotResponse(Guid NumberId, string Welcome, string OutOfHours, WhatsAppBotSchedule Schedule,
    IReadOnlyList<WhatsAppBotMenuOption> Menu, bool AskPromotions, bool OfferAccountLink, Guid? EmployeesTeamId, uint Version);

public sealed record WhatsAppBotScheduleDayInput(DayOfWeek? Day, TimeOnly? From, TimeOnly? To);

public sealed record WhatsAppBotScheduleInput(string? TimeZoneId, IReadOnlyList<WhatsAppBotScheduleDayInput>? Days);

public sealed record WhatsAppBotMenuOptionInput(Guid? Id, string? Label, WhatsAppBotAction? Action, Guid? TeamId, string? Reply);

public sealed record UpdateWhatsAppBotRequest(string? Welcome, string? OutOfHours, WhatsAppBotScheduleInput? Schedule,
    IReadOnlyList<WhatsAppBotMenuOptionInput>? Menu, bool? AskPromotions, bool? OfferAccountLink, Guid? EmployeesTeamId, uint? Version);

public sealed record PlatformWhatsAppBotResponse(string Welcome, string OutOfHours, WhatsAppBotSchedule Schedule,
    IReadOnlyList<WhatsAppBotMenuOption> Menu, uint Version);

public sealed record UpdatePlatformWhatsAppBotRequest(string? Welcome, string? OutOfHours, WhatsAppBotScheduleInput? Schedule,
    IReadOnlyList<WhatsAppBotMenuOptionInput>? Menu, uint? Version);
