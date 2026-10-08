using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppBotReplyBuilder(ICultureCatalog cultures)
{
    internal async Task<WhatsAppOutboundMessage> BuildAsync(string numberId, string destination, string appName, string? culture,
        string bodyKey, DateTime expiresAtUtc, CancellationToken ct, string? name = null,
        string? linkUrl = null, string? labelKey = null, bool choices = false)
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
            Footer: neutral || linkUrl is null && !choices ? null : WhatsAppTexts.Get("Bot.Footer", profile),
            LinkLabel: labelKey is null ? null : WhatsAppTexts.Get(labelKey, profile), LinkUrl: linkUrl,
            ReplyButtons: choices ? [new(BotButtons.SignIn, WhatsAppTexts.Get("Bot.SignIn", profile)),
                new(BotButtons.Recover, WhatsAppTexts.Get("Bot.Recover", profile))] : null,
            ReplyExpiresAtUtc: expiresAtUtc);
    }
}
