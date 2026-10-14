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
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
/// <summary>
/// La respuesta de ingreso de la Etapa 8 por el número de Plataforma: el enlace para entrar, el registro o la recuperación,
/// y la respuesta neutra a un operador (ADR 0049). Es el bot entero con las consultas apagadas y la opción del sistema
/// «Entrar a mi cuenta» con las consultas prendidas; nunca atribuye una entrada al dueño posterior del número.
/// </summary>
internal sealed class WhatsAppSignInLinkIssuer(IWhatsAppChannelSettings settings, ILoginLinkIssuer links, IOutbox outbox,
    IPublicOrigin origin, IPlatformSettingsReader platform, WhatsAppBotReplyBuilder replies)
{
    internal async Task IssueAsync(WhatsAppInboundContext context, string destination, DateTime expiresAtUtc, bool consultations, CancellationToken ct)
    {
        if (settings.PhoneNumberId is not { } numberId) return;
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
        else if ((await platform.FindAsync(ct))?.ConsumerSignup == ConsumerSignupMode.Open
            && settings.ValidateNewDestination(LoginCodeDestination.ForPhone(PhoneNumber.Create(destination).Value, "whatsapp")).IsSuccess)
        { body = "Bot.Unknown"; label = "Bot.Create"; url = Url("registro?channel=whatsapp"); }
        else { body = "Bot.Closed"; choices = true; }
        // Con las consultas prendidas el chat sirve para más que entrar: el pie de la Etapa 8 deja de ser cierto.
        var reply = await replies.BuildAsync(numberId, destination, settings.AppName, account?.Culture, body, expiresAtUtc, ct,
            name, url, label, choices, footer: !consultations);
        outbox.Enqueue("whatsapp", JsonSerializer.Serialize(reply), context.Message.OriginalUserId);
    }

    private string Url(string path) => new Uri(origin.Value ?? throw new InvalidOperationException("Public origin required."), path).AbsoluteUri;
}
