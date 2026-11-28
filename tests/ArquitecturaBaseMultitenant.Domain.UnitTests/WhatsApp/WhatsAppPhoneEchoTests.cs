using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.WhatsApp;

/// <summary>
/// KAN-44 (B10): los ecos desde la app WhatsApp Business del celular entran como salientes sin autor ni outbox, dejan el
/// chat respondido y frenan el aviso de espera sin abrir la ventana de 24 h ni cambiar quién lo tiene tomado ni su equipo.
/// </summary>
public sealed class WhatsAppPhoneEchoTests
{
    private static readonly DateTime BaseTime = new(2026, 11, 27, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Phone_echo_message_is_outbound_sent_without_author_or_outbox_and_cannot_be_reviewed()
    {
        var chatId = Guid.NewGuid();
        var text = WhatsAppChatMessage.FromPhone(chatId, "wamid.echo.text", "Ya te lo mando", null, BaseTime);
        Assert.Equal((WhatsAppChatMessageKind.Text, WhatsAppMessageDirection.Outbound, WhatsAppChatMessageStatus.Sent, true),
            (text.Kind, text.Direction, text.Status, text.SentFromPhone));
        Assert.Equal(("Ya te lo mando", "wamid.echo.text"), (text.Text, text.Wamid));
        Assert.Null(text.AuthorUserId);
        Assert.Null(text.AuthorName);
        Assert.Null(text.OutboxMessageId);
        Assert.Null(text.TemplateId);
        Assert.Throws<InvalidOperationException>(() => text.MarkReviewed(Guid.NewGuid(), "Ana", BaseTime));

        var media = WhatsAppChatMessage.FromPhone(chatId, "wamid.echo.img", null, WhatsAppMediaKind.Image, BaseTime);
        Assert.Equal((WhatsAppChatMessageKind.Media, WhatsAppMediaKind.Image, true), (media.Kind, media.MediaKind, media.SentFromPhone));
        Assert.Null(media.Text);

        var other = WhatsAppChatMessage.FromPhone(chatId, "wamid.echo.evt", null, null, BaseTime);
        Assert.Equal((WhatsAppChatMessageKind.Event, true), (other.Kind, other.SentFromPhone));
    }

    [Fact]
    public void Phone_echo_marks_chat_answered_and_clears_waiting_without_opening_window_or_changing_assignment()
    {
        var chat = WhatsAppChat.ForCustomer(Guid.NewGuid(), PhoneNumber.Create("+5491134567890").Value, Guid.NewGuid(), BaseTime);
        var teamId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        chat.RouteToTeam(teamId);
        chat.RecordInbound(BaseTime.AddMinutes(1), "Hola");
        var windowBefore = chat.WindowOpenUntilUtc;
        Assert.True(chat.AwaitingReply);
        Assert.Equal(BaseTime.AddMinutes(1), chat.WaitingSinceUtc);

        // Respuesta desde el celular con el chat sin tomar: queda respondido y sin esperar, sin tocar ventana ni equipo.
        chat.RecordPhoneEcho(BaseTime.AddMinutes(2), "Hola, te ayudo");
        Assert.False(chat.AwaitingReply);
        Assert.Null(chat.WaitingSinceUtc);
        Assert.Equal(windowBefore, chat.WindowOpenUntilUtc);
        Assert.Equal((teamId, (Guid?)null, (DateTime?)null), (chat.TeamId, chat.AssigneeUserId, chat.TakenAtUtc));
        Assert.Equal("Hola, te ayudo", chat.LastMessagePreview);

        // Si el cliente escribe de nuevo y alguien lo toma, un eco posterior mantiene a quien lo tomó y su equipo.
        chat.RecordInbound(BaseTime.AddMinutes(5), "Otra consulta");
        Assert.True(chat.Take(agentId, BaseTime.AddMinutes(6)).IsSuccess);
        var takenWindow = chat.WindowOpenUntilUtc;
        chat.RecordPhoneEcho(BaseTime.AddMinutes(10), "Listo desde el celu");
        Assert.False(chat.AwaitingReply);
        Assert.Null(chat.WaitingSinceUtc);
        Assert.Equal(takenWindow, chat.WindowOpenUntilUtc);
        Assert.Equal((teamId, agentId, BaseTime.AddMinutes(6)), (chat.TeamId, chat.AssigneeUserId, chat.TakenAtUtc));
    }

    [Fact]
    public void Phone_echo_on_brand_new_chat_keeps_window_closed_and_not_waiting()
    {
        var chat = WhatsAppChat.ForCustomer(Guid.NewGuid(), PhoneNumber.Create("+5491134567891").Value, Guid.NewGuid(), BaseTime);
        chat.RecordPhoneEcho(BaseTime, "Te escribo desde el celular");
        Assert.Null(chat.LastInboundAtUtc);
        Assert.Null(chat.WindowOpenUntilUtc);
        Assert.False(chat.IsWindowOpen(BaseTime));
        Assert.False(chat.AwaitingReply);
        Assert.Null(chat.WaitingSinceUtc);
        Assert.Equal("Te escribo desde el celular", chat.LastMessagePreview);
    }

    [Fact]
    public void Out_of_order_phone_echo_older_than_latest_inbound_does_not_clear_awaiting_reply()
    {
        var chat = WhatsAppChat.ForCustomer(Guid.NewGuid(), PhoneNumber.Create("+5491134567892").Value, Guid.NewGuid(), BaseTime);
        chat.RecordInbound(BaseTime.AddMinutes(5), "Último mensaje del cliente");
        chat.RecordPhoneEcho(BaseTime.AddMinutes(2), "Eco atrasado");
        Assert.True(chat.AwaitingReply);
        Assert.Equal(BaseTime.AddMinutes(5), chat.WaitingSinceUtc);
        Assert.Equal("Último mensaje del cliente", chat.LastMessagePreview);
    }

    [Fact]
    public void Same_second_phone_echo_answers_a_chat_created_milliseconds_later()
    {
        var inboundAt = BaseTime;
        var chat = WhatsAppChat.ForCustomer(Guid.NewGuid(), PhoneNumber.Create("+5491134567893").Value, Guid.NewGuid(), BaseTime.AddMilliseconds(450));
        chat.RecordInbound(inboundAt, "Hola");
        chat.RecordPhoneEcho(inboundAt, "Te respondo en el acto");
        Assert.False(chat.AwaitingReply);
        Assert.Null(chat.WaitingSinceUtc);
        Assert.Equal("Te respondo en el acto", chat.LastMessagePreview);
    }
}
