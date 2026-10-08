using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
/// <summary>Efectos y reply en la UoW preparada por T06; nunca atribuye una entrada al dueño posterior.</summary>
internal sealed class WhatsAppBotWriter(IWhatsAppChannelSettings settings, ILoginLinkIssuer links, IOutbox outbox,
    IPublicOrigin origin, IPlatformSettingsReader platform, WhatsAppBotReplyBuilder replies,
    TimeProvider clock) : IWhatsAppInboundConsumer
{
    public async Task ConsumeAsync(WhatsAppInboundContext context, CancellationToken ct)
    {
        if (!settings.IsEnabled || settings.PhoneNumberId is not { } numberId || context.Contact is not { } contact
            || context.Message.Kind is not (WhatsAppMessageKind.Text or WhatsAppMessageKind.Interactive)
            || context.Message.Direction != WhatsAppMessageDirection.Inbound) return;
        var expiresAtUtc = contact.LastInboundAtUtc?.AddHours(24);
        if (expiresAtUtc is null || clock.GetUtcNow().UtcDateTime >= expiresAtUtc) return;
        var account = context.OriginalAccount;
        string body; string? label = null; string? url = null; string? name = null; var choices = false;
        if (context.Message.OriginalUserId is not null)
        {
            if (account?.IsPlatformOperator == true) body = "Bot.OperatorNeutralReply";
            else if (!context.OriginalMethodAvailable || account is null || context.OriginalMethod is null)
                body = "Bot.Disabled";
            else if (account.Status is UserStatus.Suspended or UserStatus.Deleted)
                body = "Bot.Disabled";
            else
            {
                var issued = await links.IssueAsync(account.Id, context.OriginalMethod.Id, ct);
                if (issued.IsFailure) body = issued.Error.Code == LoginLinkErrors.TooManyRequestsCode ? "Bot.Limit" : "Bot.Disabled";
                else
                {
                    body = account.DisplayName is null ? "Bot.LinkWithoutName" : "Bot.Link"; name = account.DisplayName;
                    label = "Bot.Enter";
                    url = Url("login/enlace") + "#t=" + issued.Value.Token;
                }
            }
        }
        else if (context.Message.ReplyId is BotButtons.SignIn or BotButtons.Recover)
        {
            body = "Bot.Web";
            label = context.Message.ReplyId == BotButtons.SignIn ? "Bot.SignIn" : "Bot.Recover";
            url = Url(context.Message.ReplyId == BotButtons.SignIn ? "login" : "recuperar");
        }
        else if ((await platform.FindAsync(ct))?.ConsumerSignup == ConsumerSignupMode.Open)
        { body = "Bot.Unknown"; label = "Bot.Create"; url = Url("registro"); }
        else { body = "Bot.Closed"; choices = true; }
        var reply = await replies.BuildAsync(numberId, contact.WaId, settings.AppName, account?.Culture, body, expiresAtUtc.Value, ct,
            name, url, label, choices);
        outbox.Enqueue("whatsapp", JsonSerializer.Serialize(reply), context.Message.OriginalUserId);
    }
    private string Url(string path) => new Uri(origin.Value ?? throw new InvalidOperationException("Public origin required."), path).AbsoluteUri;
}
