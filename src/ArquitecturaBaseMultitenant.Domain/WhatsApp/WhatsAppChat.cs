using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Conversación de la organización en una bandeja: Clientes, con su contacto, o Internos, con el miembro activo cuyo
/// teléfono verificado escribió (ADR 0038). Una por número, remitente y bandeja.
/// </summary>
public sealed class WhatsAppChat : WhatsAppChatBase, ITenantOwned
{
    private WhatsAppChat() { }
    public Guid TenantId { get; private set; }
    public WhatsAppInbox Inbox { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Guid? MemberUserId { get; private set; }

    public static WhatsAppChat ForCustomer(Guid channelId, PhoneNumber waId, Guid customerId, DateTime nowUtc)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("A customer is required.", nameof(customerId));
        var chat = new WhatsAppChat { Inbox = WhatsAppInbox.Customers, CustomerId = customerId };
        chat.Initialize(channelId, waId, nowUtc);
        return chat;
    }

    public static WhatsAppChat ForMember(Guid channelId, PhoneNumber waId, Guid memberUserId, DateTime nowUtc)
    {
        if (memberUserId == Guid.Empty) throw new ArgumentException("A member is required.", nameof(memberUserId));
        var chat = new WhatsAppChat { Inbox = WhatsAppInbox.Internal, MemberUserId = memberUserId };
        chat.Initialize(channelId, waId, nowUtc);
        return chat;
    }
}
