using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Una consulta al número de Plataforma (ADR 0038, precisión del 03/10): sin bandejas, la atienden los operadores.</summary>
public sealed class PlatformWhatsAppChat : WhatsAppChatBase
{
    private PlatformWhatsAppChat() { }

    public static PlatformWhatsAppChat Create(Guid channelId, PhoneNumber waId, DateTime nowUtc)
    {
        var chat = new PlatformWhatsAppChat();
        chat.Initialize(channelId, waId, nowUtc);
        return chat;
    }
}
