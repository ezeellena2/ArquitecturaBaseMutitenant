using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El bot de un número de la organización, dentro de la transacción de la entrada (ADR 0038). Una conversación empieza
/// cuando la ventana de 24 horas estaba cerrada: los empleados pasan directo a su equipo, sin menú; los clientes reciben la
/// bienvenida o «Fuera de horario» con el menú. Elegir una opción la pasa a un equipo o contesta su texto, y ahí termina la
/// parte del bot: si «Además» lo pide, pregunta una sola vez por promociones. Lo que dice el bot queda en la conversación,
/// sin autor. Nunca responde fuera de las 24 horas ni desde un número desconectado (D7).
/// </summary>
internal sealed class WhatsAppAttentionBotWriter(IWhatsAppBotRepository bots, WhatsAppBotResolver resolver, IWhatsAppChatRepository chats,
    ITimeZoneService zones, ITenantSettingsReader settings, WhatsAppBotReplyBuilder replies, IOutbox outbox, TimeProvider clock)
{
    internal async Task RespondAsync(WhatsAppInboundChannel channel, WhatsAppChat chat, WhatsAppCustomer? customer, bool newConversation,
        string? replyId, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (channel.Status == WhatsAppNumberStatus.Disconnected || chat.WindowOpenUntilUtc is not { } expiresAtUtc || nowUtc >= expiresAtUtc) return;
        var bot = await bots.GetAsync(channel.Id, null, ct) ?? await resolver.OrganizationAsync(channel.Id, ct);
        if (customer is null)
        {
            if (newConversation && bot.EmployeesTeamId is { } team) chat.RouteToTeam(team);
            return;
        }
        var turn = new Turn(channel, chat, await replies.CultureAsync((await settings.FindCurrentAsync(ct))?.DefaultCulture, ct), expiresAtUtc, nowUtc);
        if (replyId is BotButtons.PromotionsYes or BotButtons.PromotionsNo)
        {
            var accepted = replyId == BotButtons.PromotionsYes;
            if (customer.ChangeConsent(accepted ? WhatsAppConsent.Accepted : WhatsAppConsent.Declined, WhatsAppConsentSource.Bot, nowUtc))
                Send(turn, WhatsAppTexts.Get(accepted ? "Bot.PromotionsAccepted" : "Bot.PromotionsDeclined", turn.Profile));
            return;
        }
        // Una persona ya la atiende o la espera un equipo: el bot no se mete.
        if (chat.IsTaken || chat.TeamId is not null) return;
        if (BotButtons.ParseOption(replyId) is { } optionId && bot.FindOption(optionId) is { } option)
        {
            if (option.Action == WhatsAppBotAction.Team) chat.RouteToTeam(option.TeamId!.Value);
            else Send(turn, option.Reply!);
            AskPromotions(turn, bot, customer);
            return;
        }
        if (!newConversation) return;
        var body = bot.IsOpenAt(zones.ConvertToLocal(nowUtc, bot.TimeZoneId)) ? bot.Welcome : bot.OutOfHours;
        if (bot.Options.Count > 0) { Send(turn, body, rows: [.. WhatsAppBotReplyBuilder.Rows(bot.Options)]); return; }
        Send(turn, body);
        AskPromotions(turn, bot, customer);
    }

    private void AskPromotions(Turn turn, WhatsAppBot bot, WhatsAppCustomer customer)
    {
        if (!bot.AskPromotions || customer.Consent != WhatsAppConsent.NoAnswer || customer.PromotionsAskedAtUtc is not null) return;
        Send(turn, WhatsAppTexts.Get("Bot.PromotionsQuestion", turn.Profile), buttons:
            [new(BotButtons.PromotionsYes, WhatsAppTexts.Get("Bot.PromotionsYes", turn.Profile)),
                new(BotButtons.PromotionsNo, WhatsAppTexts.Get("Bot.PromotionsNo", turn.Profile))]);
        customer.MarkPromotionsAsked(turn.NowUtc);
    }

    private void Send(Turn turn, string body, IReadOnlyList<WhatsAppReplyButton>? rows = null, IReadOnlyList<WhatsAppReplyButton>? buttons = null)
    {
        var message = WhatsAppBotReplyBuilder.Attention(turn.Channel, turn.Chat.WaId, body, turn.ExpiresAtUtc, turn.Profile, rows, buttons);
        var outboxId = outbox.Enqueue("whatsapp", JsonSerializer.Serialize(message));
        chats.Add(WhatsAppChatMessage.Outbound(turn.Chat.Id, WhatsAppChatMessageKind.Text, null, null, body, outboxId, null, turn.NowUtc));
        turn.Chat.RecordBotReply(turn.NowUtc, body);
    }

    private sealed record Turn(WhatsAppInboundChannel Channel, WhatsAppChat Chat, CultureProfile Profile, DateTime ExpiresAtUtc, DateTime NowUtc);
}
