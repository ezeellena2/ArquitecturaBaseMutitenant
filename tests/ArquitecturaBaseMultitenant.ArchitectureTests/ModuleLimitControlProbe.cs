using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.Application.Modules.Control.Services;

/// <summary>
/// El control negativo de ModuleLimitArchitectureTests: un módulo de prueba que lee los máximos de la plataforma de la IA y
/// de WhatsApp sin pasar por el resolvedor, y que nombra la configuración de WhatsApp y la clave de su tope.
/// </summary>
internal static class ControlForeignLimitReader
{
    public const string ChatRetentionKey = "whatsapp.chatRetentionDays";

    public static int DailyMessages(PlatformAssistantSettingsResponse settings) => settings.DailyMessageLimit;

    public static int ChatRetention(WhatsAppPlatformSettingsRow settings) => settings.MaxOrganizationChatRetentionDays;

    public static string ChatRetentionLimit() => ChatRetentionKey;
}
