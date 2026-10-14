using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El bot del número de Plataforma con las consultas prendidas (ADR 0038, precisión del 03/10), sin «Además». El menú
/// empieza por «Entrar a mi cuenta», que manda la respuesta de ingreso de la Etapa 8 con sus ramas (ADR 0049); esa
/// respuesta no queda en la conversación, porque puede llevar un enlace para entrar.
/// </summary>
internal sealed class PlatformWhatsAppBotWriter(IPlatformWhatsAppBotRepository bots, WhatsAppBotResolver resolver,
    IPlatformWhatsAppChatRepository chats, WhatsAppSignInLinkIssuer signIn, ITimeZoneService zones, WhatsAppBotReplyBuilder replies,
    IOutbox outbox, TimeProvider clock)
{
    internal async Task RespondAsync(WhatsAppInboundContext context, PlatformWhatsAppChat chat, bool newConversation, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (context.Channel is not { } channel || chat.WindowOpenUntilUtc is not { } expiresAtUtc || nowUtc >= expiresAtUtc) return;
        var replyId = context.Message.ReplyId;
        var optionId = BotButtons.ParseOption(replyId);
        if (optionId == PlatformWhatsAppBot.SignInOptionId || replyId is BotButtons.SignIn or BotButtons.Recover)
        {
            await signIn.IssueAsync(context, chat.WaId, expiresAtUtc, consultations: true, ct);
            return;
        }
        if (chat.IsTaken || chat.TeamId is not null) return;
        var bot = await bots.GetAsync(null, ct) ?? await resolver.PlatformAsync(ct);
        var profile = await replies.CultureAsync(context.OriginalAccount?.Culture, ct);
        if (optionId is { } id && bot.FindOption(id) is { } option)
        {
            if (option.Action == WhatsAppBotAction.Team) chat.RouteToTeam(option.TeamId!.Value);
            else Send(channel, chat, option.Reply!, expiresAtUtc, profile, nowUtc);
            return;
        }
        if (!newConversation) return;
        var body = bot.IsOpenAt(zones.ConvertToLocal(nowUtc, bot.TimeZoneId)) ? bot.Welcome : bot.OutOfHours;
        Send(channel, chat, body, expiresAtUtc, profile, nowUtc,
            [new(BotButtons.Option(PlatformWhatsAppBot.SignInOptionId), WhatsAppTexts.Get("Bot.SignInOption", profile)),
                .. WhatsAppBotReplyBuilder.Rows(bot.Options)]);
    }

    private void Send(WhatsAppInboundChannel channel, PlatformWhatsAppChat chat, string body, DateTime expiresAtUtc,
        Common.Formatting.CultureProfile profile, DateTime nowUtc, IReadOnlyList<WhatsAppReplyButton>? rows = null)
    {
        var message = WhatsAppBotReplyBuilder.Attention(channel, chat.WaId, body, expiresAtUtc, profile, rows);
        var outboxId = outbox.Enqueue("whatsapp", JsonSerializer.Serialize(message));
        chats.Add(PlatformWhatsAppChatMessage.Outbound(chat.Id, WhatsAppChatMessageKind.Text, null, null, body, outboxId, null, nowUtc));
        chat.RecordBotReply(nowUtc, body);
    }
}
