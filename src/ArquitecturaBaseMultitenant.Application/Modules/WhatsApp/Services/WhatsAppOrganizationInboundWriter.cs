using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// La entrada a un número de la organización, dentro de su alcance y de la transacción del procesador (decisión 4 de la
/// E11): un miembro activo con ese teléfono verificado va a Internos; los demás, a Clientes con su contacto. Una entrada
/// con la ventana de 24 horas cerrada empieza otra conversación, que vuelve a pasar por el bot salvo que responda a una
/// plantilla enviada por un operador en las últimas 24 horas (B11). La copia global se purga al pasar a las tablas de la
/// organización.
/// </summary>
internal sealed class WhatsAppOrganizationInboundWriter(IWhatsAppChatRepository chats, IWhatsAppCustomerRepository customers,
    IActiveMemberPhoneReader members, IPayloadProtector protector, WhatsAppAttentionBotWriter bot, WhatsAppConversationLinkWriter links,
    WhatsAppBroadcastRecipientWriter recipients, TimeProvider clock) : IWhatsAppInboundConsumer
{
    public async Task ConsumeAsync(WhatsAppInboundContext context, CancellationToken ct)
    {
        if (context.Channel is not { OrganizationId: not null } channel || context.Contact is not { } contact) return;
        if (context.Message.IsEcho) { await ConsumeEchoAsync(channel, contact, context.Message, ct); return; }
        if (context.Message.Direction != WhatsAppMessageDirection.Inbound) return;
        var message = context.Message;
        if (await chats.HasInboundAsync(message.Wamid, ct)) { message.ClearPersonalData(); return; }
        var waId = PhoneNumber.Create(contact.WaId).Value;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var occurredAtUtc = message.OccurredAtUtc > nowUtc ? nowUtc : message.OccurredAtUtc;
        var text = message.EncryptedText is { } cipher ? protector.Unprotect(cipher) : null;
        var media = MediaKind(message.ContentType);
        var member = await members.FindMemberUserIdAsync(waId, ct);
        await chats.LockAsync(channel.Id, waId.Value, ct);
        var chat = await chats.GetAsync(channel.Id, waId.Value, member is null ? WhatsAppInbox.Customers : WhatsAppInbox.Internal, ct);
        WhatsAppCustomer? customer = null;
        if (member is null) customer = await CustomerAsync(waId, nowUtc, ct);
        // Un contacto sin nombre toma el de su perfil una sola vez (C14 de la E12); el dato se vacía abajo con el resto.
        if (customer is not null && message.EncryptedProfileName is { } profile) customer.TakeProfileName(protector.Unprotect(profile), nowUtc);
        var newConversation = chat is null || !chat.IsWindowOpen(occurredAtUtc);
        var continuesWithOperator = chat is not null && newConversation
            && await chats.HasOperatorTemplateSinceAsync(chat.Id, occurredAtUtc - WhatsAppChatBase.Window, ct);
        var startsWithBot = newConversation && !continuesWithOperator;
        if (chat is null)
        {
            chat = customer is null ? WhatsAppChat.ForMember(channel.Id, waId, member!.Value, nowUtc)
                : WhatsAppChat.ForCustomer(channel.Id, waId, customer.Id, nowUtc);
            chats.Add(chat);
        }
        else if (startsWithBot) chat.RestartWithBot();
        customer?.RecordConversation(occurredAtUtc);
        chats.Add(WhatsAppChatMessage.Inbound(chat.Id, message.Wamid, text, media, occurredAtUtc));
        chat.RecordInbound(occurredAtUtc, text);
        await links.CopyAsync(chat.Id, WhatsAppMessageDirection.Inbound, text, media, occurredAtUtc, message.OriginalUserId, ct);
        // Una difusión: la baja por su botón o la respuesta del contacto (B6).
        if (customer is not null) await recipients.RecordInboundAsync(customer, message.ReplyId == BotButtons.Unsubscribe, occurredAtUtc, ct);
        await bot.RespondAsync(context, chat, customer, startsWithBot, ct);
        message.ClearPersonalData();
    }

    /// <summary>
    /// Un eco enviado desde la app WhatsApp Business del celular en coexistencia (B10): entra al chat como mensaje saliente sin
    /// autor ni outbox, deja el chat respondido y frena el aviso de espera, sin bot, sin abrir la ventana de 24 h y sin cambiar
    /// quién lo tiene tomado ni su equipo.
    /// </summary>
    private async Task ConsumeEchoAsync(WhatsAppInboundChannel channel, WhatsAppContact contact, WhatsAppMessage message, CancellationToken ct)
    {
        if (await chats.HasInboundAsync(message.Wamid, ct)) { message.ClearPersonalData(); return; }
        var waId = PhoneNumber.Create(contact.WaId).Value;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var occurredAtUtc = message.OccurredAtUtc > nowUtc ? nowUtc : message.OccurredAtUtc;
        var text = message.EncryptedText is { } cipher ? protector.Unprotect(cipher) : null;
        var media = MediaKind(message.ContentType);
        var member = await members.FindMemberUserIdAsync(waId, ct);
        await chats.LockAsync(channel.Id, waId.Value, ct);
        var chat = await chats.GetAsync(channel.Id, waId.Value, member is null ? WhatsAppInbox.Customers : WhatsAppInbox.Internal, ct);
        WhatsAppCustomer? customer = null;
        if (member is null) customer = await CustomerAsync(waId, nowUtc, ct);
        if (chat is null)
        {
            chat = customer is null ? WhatsAppChat.ForMember(channel.Id, waId, member!.Value, occurredAtUtc)
                : WhatsAppChat.ForCustomer(channel.Id, waId, customer.Id, occurredAtUtc);
            chats.Add(chat);
        }
        chats.Add(WhatsAppChatMessage.FromPhone(chat.Id, message.Wamid, text, media, occurredAtUtc));
        chat.RecordPhoneEcho(occurredAtUtc, text);
        await links.CopyAsync(chat.Id, WhatsAppMessageDirection.Outbound, text, media, occurredAtUtc, null, ct);
        message.ClearPersonalData();
    }

    private async Task<WhatsAppCustomer> CustomerAsync(PhoneNumber waId, DateTime nowUtc, CancellationToken ct)
    {
        await customers.LockAsync(ct);
        if (await customers.GetByWaIdAsync(waId.Value, ct) is { } existing) return existing;
        var created = WhatsAppCustomer.Create(waId, nowUtc);
        customers.Add(created);
        return created;
    }

    /// <summary>El tipo que manda Meta para lo que no es texto: se informa sin el archivo (D7).</summary>
    internal static WhatsAppMediaKind? MediaKind(string? contentType) => contentType switch
    {
        null => null,
        "image" => WhatsAppMediaKind.Image,
        "audio" or "voice" => WhatsAppMediaKind.Audio,
        "video" => WhatsAppMediaKind.Video,
        "document" => WhatsAppMediaKind.Document,
        "sticker" => WhatsAppMediaKind.Sticker,
        "location" => WhatsAppMediaKind.Location,
        _ => WhatsAppMediaKind.Other,
    };
}
