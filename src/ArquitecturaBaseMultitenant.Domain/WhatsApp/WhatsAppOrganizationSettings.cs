using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// La configuración de WhatsApp de una organización (decisión 272): cuánto se guardan los mensajes de sus chats, de 1 al
/// máximo que fija la plataforma. Privada de la organización; sin fila vale el valor por defecto.
/// </summary>
public sealed class WhatsAppOrganizationSettings : Entity, ITenantOwned, IAuditable, IVersioned
{
    private WhatsAppOrganizationSettings() { }

    public Guid TenantId { get; private set; }
    public int ChatRetentionDays { get; private set; } = WhatsAppSettingsLimits.DefaultChatRetentionDays;
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    public static WhatsAppOrganizationSettings Create() => new();

    /// <summary>Cambia el plazo; nada cambia si está fuera de 1 y el máximo de la plataforma.</summary>
    public bool Update(int chatRetentionDays, int maxChatRetentionDays)
    {
        if (chatRetentionDays < WhatsAppSettingsLimits.MinChatRetentionDays || chatRetentionDays > maxChatRetentionDays) return false;
        ChatRetentionDays = chatRetentionDays;
        return true;
    }
}
