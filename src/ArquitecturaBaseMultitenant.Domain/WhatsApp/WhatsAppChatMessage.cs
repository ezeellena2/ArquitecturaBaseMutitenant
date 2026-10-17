using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Un mensaje de una conversación de la organización. El texto se guarda mientras exista el contacto (D4).</summary>
public sealed class WhatsAppChatMessage : WhatsAppChatMessageBase, ITenantOwned
{
    private WhatsAppChatMessage() { }
    public Guid TenantId { get; private set; }

    public static WhatsAppChatMessage Inbound(Guid chatId, string wamid, string? text, WhatsAppMediaKind? mediaKind, DateTime occurredAtUtc)
    {
        var message = new WhatsAppChatMessage();
        message.InitializeInbound(chatId, wamid, text, mediaKind, occurredAtUtc);
        return message;
    }

    public static WhatsAppChatMessage Outbound(Guid chatId, WhatsAppChatMessageKind kind, Guid? authorUserId, string? authorName, string text,
        Guid outboxMessageId, Guid? templateId, DateTime nowUtc)
    {
        var message = new WhatsAppChatMessage();
        message.InitializeOutbound(chatId, kind, authorUserId, authorName, text, outboxMessageId, templateId, nowUtc);
        return message;
    }

    public static WhatsAppChatMessage Note(Guid chatId, Guid authorUserId, string? authorName, string text, DateTime nowUtc)
    {
        var message = new WhatsAppChatMessage();
        message.InitializeNote(chatId, authorUserId, authorName, text, nowUtc);
        return message;
    }
}
