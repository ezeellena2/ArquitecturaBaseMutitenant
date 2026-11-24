namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Los plazos de la configuración de WhatsApp (decisiones 270 y 272): cuánto se guardan los chats de la plataforma y de cada
/// organización, y el máximo que Plataforma les deja elegir. Sin valor guardado, valen los de defecto.
/// </summary>
public static class WhatsAppSettingsLimits
{
    public const int DefaultChatRetentionDays = 90;
    public const int MinChatRetentionDays = 1;
    public const int MaxChatRetentionDays = 3650;

    /// <summary>El máximo que una organización puede elegir para sus chats, si Plataforma no lo cambió.</summary>
    public const int DefaultMaxOrganizationChatRetentionDays = 3650;
}
