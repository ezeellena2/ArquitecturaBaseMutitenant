namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Un mensaje de una consulta al número de Plataforma; su texto se guarda mientras exista la conversación (D4).</summary>
public sealed class PlatformWhatsAppChatMessage : WhatsAppChatMessageBase
{
    private PlatformWhatsAppChatMessage() { }

    public static PlatformWhatsAppChatMessage Inbound(Guid chatId, string wamid, string? text, WhatsAppMediaKind? mediaKind, DateTime occurredAtUtc)
    {
        var message = new PlatformWhatsAppChatMessage();
        message.InitializeInbound(chatId, wamid, text, mediaKind, occurredAtUtc);
        return message;
    }

    public static PlatformWhatsAppChatMessage Outbound(Guid chatId, WhatsAppChatMessageKind kind, Guid? authorUserId, string? authorName,
        string text, Guid outboxMessageId, Guid? templateId, DateTime nowUtc)
    {
        var message = new PlatformWhatsAppChatMessage();
        message.InitializeOutbound(chatId, kind, authorUserId, authorName, text, outboxMessageId, templateId, nowUtc);
        return message;
    }

    public static PlatformWhatsAppChatMessage Note(Guid chatId, Guid authorUserId, string? authorName, string text, DateTime nowUtc)
    {
        var message = new PlatformWhatsAppChatMessage();
        message.InitializeNote(chatId, authorUserId, authorName, text, nowUtc);
        return message;
    }
}
