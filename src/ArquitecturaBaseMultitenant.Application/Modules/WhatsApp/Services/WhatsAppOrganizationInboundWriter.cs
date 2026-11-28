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
/// organización. Por la misma cola llegan los ecos del celular (B10) y el motivo de un envío que Meta informó fallido (B5).
/// </summary>
internal sealed class WhatsAppOrganizationInboundWriter(IWhatsAppChatRepository chats, IWhatsAppCustomerRepository customers,
    IActiveMemberPhoneReader members, IPayloadProtector protector, WhatsAppAttentionBotWriter bot, WhatsAppConversationLinkWriter links,
    WhatsAppBroadcastRecipientWriter recipients, WhatsAppDeliveryFailureWriter failures, TimeProvider clock) : IWhatsAppInboundConsumer
{
    public async Task ConsumeAsync(WhatsAppInboundContext context, CancellationToken ct)
    {
        if (context.Channel is not { OrganizationId: not null } channel) return;
        var message = context.Message;
        if (message.IsDeliveryFailure) { await failures.RecordOrganizationAsync(message, ct); return; }
        if (context.Contact is not { } contact || !message.IsEcho && message.Direction != WhatsAppMessageDirection.Inbound) return;
        if (await chats.HasInboundAsync(message.Wamid, ct)) { message.ClearPersonalData(); return; }
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var entry = await LocateAsync(channel, contact, message, nowUtc, ct);
        if (message.IsEcho) ConsumeEcho(channel, entry, message);
        else await ConsumeInboundAsync(context, channel, entry, nowUtc, ct);
        await links.CopyAsync(entry.Chat!.Id, message.Direction, entry.Text, entry.Media, entry.OccurredAtUtc,
            message.IsEcho ? null : message.OriginalUserId, ct);
        if (!message.IsEcho)
        {
            // Una difusión: la baja por su botón o la respuesta del contacto (B6).
            if (entry.Customer is not null)
                await recipients.RecordInboundAsync(entry.Customer, message.ReplyId == BotButtons.Unsubscribe, entry.OccurredAtUtc, ct);
            await bot.RespondAsync(context, entry.Chat, entry.Customer, entry.StartsWithBot, ct);
        }
        message.ClearPersonalData();
    }

    private async Task ConsumeInboundAsync(WhatsAppInboundContext context, WhatsAppInboundChannel channel, Entry entry, DateTime nowUtc,
        CancellationToken ct)
    {
        var message = context.Message;
        // Un contacto sin nombre toma el de su perfil una sola vez (C14 de la E12); el dato se vacía con el resto.
        if (entry.Customer is not null && message.EncryptedProfileName is { } profile)
            entry.Customer.TakeProfileName(protector.Unprotect(profile), nowUtc);
        var chat = entry.Chat;
        var newConversation = chat is null || !chat.IsWindowOpen(entry.OccurredAtUtc);
        var continuesWithOperator = chat is not null && newConversation
            && await chats.HasOperatorTemplateSinceAsync(chat.Id, entry.OccurredAtUtc - WhatsAppChatBase.Window, ct);
        entry.StartsWithBot = newConversation && !continuesWithOperator;
        if (chat is null) chat = entry.Chat = Open(channel, entry, nowUtc);
        else if (entry.StartsWithBot) chat.RestartWithBot();
        entry.Customer?.RecordConversation(entry.OccurredAtUtc);
        chats.Add(WhatsAppChatMessage.Inbound(chat.Id, message.Wamid, entry.Text, entry.Media, entry.OccurredAtUtc));
        chat.RecordInbound(entry.OccurredAtUtc, entry.Text);
    }

    /// <summary>
    /// Un eco enviado desde la app WhatsApp Business del celular en coexistencia (B10): entra al chat como mensaje saliente sin
    /// autor ni outbox, deja el chat respondido y frena el aviso de espera, sin bot, sin abrir la ventana de 24 h y sin cambiar
    /// quién lo tiene tomado ni su equipo.
    /// </summary>
    private void ConsumeEcho(WhatsAppInboundChannel channel, Entry entry, WhatsAppMessage message)
    {
        var chat = entry.Chat ??= Open(channel, entry, entry.OccurredAtUtc);
        chats.Add(WhatsAppChatMessage.FromPhone(chat.Id, message.Wamid, entry.Text, entry.Media, entry.OccurredAtUtc));
        chat.RecordPhoneEcho(entry.OccurredAtUtc, entry.Text);
    }

    /// <summary>
    /// Lo común de lo entrante y de un eco: la bandeja (Internos si el teléfono es de un miembro activo, si no Clientes con su
    /// contacto), el chat bajo el lock del número y del remitente, el texto y la hora, que nunca queda en el futuro.
    /// </summary>
    private async Task<Entry> LocateAsync(WhatsAppInboundChannel channel, WhatsAppContact contact, WhatsAppMessage message,
        DateTime nowUtc, CancellationToken ct)
    {
        var waId = PhoneNumber.Create(contact.WaId).Value;
        var member = await members.FindMemberUserIdAsync(waId, ct);
        await chats.LockAsync(channel.Id, waId.Value, ct);
        var chat = await chats.GetAsync(channel.Id, waId.Value, member is null ? WhatsAppInbox.Customers : WhatsAppInbox.Internal, ct);
        var customer = member is null ? await CustomerAsync(waId, nowUtc, ct) : null;
        return new Entry(waId, member, customer, chat, message.OccurredAtUtc > nowUtc ? nowUtc : message.OccurredAtUtc,
            message.EncryptedText is { } cipher ? protector.Unprotect(cipher) : null, MediaKind(message.ContentType));
    }

    private WhatsAppChat Open(WhatsAppInboundChannel channel, Entry entry, DateTime startedAtUtc)
    {
        var chat = entry.Customer is null ? WhatsAppChat.ForMember(channel.Id, entry.WaId, entry.Member!.Value, startedAtUtc)
            : WhatsAppChat.ForCustomer(channel.Id, entry.WaId, entry.Customer.Id, startedAtUtc);
        chats.Add(chat);
        return chat;
    }

    private sealed class Entry(PhoneNumber waId, Guid? member, WhatsAppCustomer? customer, WhatsAppChat? chat, DateTime occurredAtUtc,
        string? text, WhatsAppMediaKind? media)
    {
        public PhoneNumber WaId { get; } = waId;
        public Guid? Member { get; } = member;
        public WhatsAppCustomer? Customer { get; } = customer;
        public WhatsAppChat? Chat { get; set; } = chat;
        public DateTime OccurredAtUtc { get; } = occurredAtUtc;
        public string? Text { get; } = text;
        public WhatsAppMediaKind? Media { get; } = media;
        public bool StartsWithBot { get; set; }

        public override string ToString() => nameof(Entry);
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
