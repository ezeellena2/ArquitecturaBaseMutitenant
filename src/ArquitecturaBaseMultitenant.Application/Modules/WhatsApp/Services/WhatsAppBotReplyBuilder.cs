using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppBotReplyBuilder(ICultureCatalog cultures)
{
    internal async Task<WhatsAppOutboundMessage> BuildAsync(string numberId, string destination, string appName, string? culture,
        string bodyKey, DateTime expiresAtUtc, CancellationToken ct, string? name = null,
        string? linkUrl = null, string? labelKey = null, bool choices = false, bool footer = true)
    {
        var profile = await new CultureProfiles(cultures).LoadAsync(culture, ct);
        var body = bodyKey switch
        {
            "Bot.Link" => WhatsAppTexts.Format(bodyKey, profile, name!, appName),
            "Bot.LinkWithoutName" or "Bot.Unknown" or "Bot.Closed" => WhatsAppTexts.Format(bodyKey, profile, appName),
            _ => WhatsAppTexts.Get(bodyKey, profile),
        };
        var neutral = bodyKey == "Bot.OperatorNeutralReply";
        return new(numberId, destination, string.Empty, profile.Entry.LanguageCode, [], Body: body,
            Footer: neutral || !footer || linkUrl is null && !choices ? null : WhatsAppTexts.Get("Bot.Footer", profile),
            LinkLabel: labelKey is null ? null : WhatsAppTexts.Get(labelKey, profile), LinkUrl: linkUrl,
            ReplyButtons: choices ? [new(BotButtons.SignIn, WhatsAppTexts.Get("Bot.SignIn", profile)),
                new(BotButtons.Recover, WhatsAppTexts.Get("Bot.Recover", profile))] : null,
            ReplyExpiresAtUtc: expiresAtUtc);
    }

    internal Task<CultureProfile> CultureAsync(string? culture, CancellationToken ct) => new CultureProfiles(cultures).LoadAsync(culture, ct);

    /// <summary>
    /// Un mensaje del bot de atención por el número que recibió la entrada: un texto, el menú como lista (con su botón
    /// «Ver opciones») o botones de respuesta. Sale solo dentro de las 24 horas; vencidas, el envío lo descarta.
    /// </summary>
    internal static WhatsAppOutboundMessage Attention(WhatsAppInboundChannel channel, string destination, string body, DateTime expiresAtUtc,
        CultureProfile profile, IReadOnlyList<WhatsAppReplyButton>? rows = null, IReadOnlyList<WhatsAppReplyButton>? buttons = null) =>
        new(channel.PhoneNumberId, destination, string.Empty, profile.Entry.LanguageCode, [], Body: body, ReplyButtons: buttons,
            ReplyExpiresAtUtc: expiresAtUtc, ChannelId: channel.Id,
            ListButton: rows is null ? null : WhatsAppTexts.Get("Bot.MenuButton", profile), ListRows: rows);

    internal static IEnumerable<WhatsAppReplyButton> Rows(IEnumerable<WhatsAppBotOption> options) =>
        options.Select(option => new WhatsAppReplyButton(BotButtons.Option(option.Id), option.Label));
}
