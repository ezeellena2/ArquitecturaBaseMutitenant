namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

/// <summary>
/// La configuración de WhatsApp de la plataforma (B18): los países que reciben códigos (al menos uno, de los habilitados y
/// sin repetir), cuánto se guardan sus chats y el máximo que puede elegir una organización.
/// </summary>
public sealed record PlatformWhatsAppSettingsResponse(IReadOnlyList<string> AllowedCountries, int ChatRetentionDays,
    int MaxOrganizationChatRetentionDays, uint Version);

public sealed record UpdatePlatformWhatsAppSettingsRequest(IReadOnlyList<string>? AllowedCountries, int? ChatRetentionDays,
    int? MaxOrganizationChatRetentionDays, uint? Version);

/// <summary>
/// La configuración de WhatsApp de la organización (B18): cuánto se guardan sus chats y el máximo que fija la plataforma, que
/// la pantalla nombra en la (i) y en el error.
/// </summary>
public sealed record WhatsAppOrganizationSettingsResponse(int ChatRetentionDays, int MaxOrganizationChatRetentionDays, uint Version);

public sealed record UpdateWhatsAppOrganizationSettingsRequest(int? ChatRetentionDays, uint? Version);
