using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Messaging;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
/// <summary>
/// La respuesta de ingreso por el número de Plataforma (E8; desde la base de la 12, sin enlace secreto, 179). Una cuenta
/// vinculada recibe el botón «Entrar a mi cuenta», que abre el ingreso, donde el código se pide desde la web; un operador, la
/// respuesta neutra, sin enlace ni mención a Plataforma (ADR 0049); un número sin cuenta, el registro web o, con el registro
/// cerrado, ingresar o recuperar la cuenta. Es el bot entero con las consultas apagadas y la opción del sistema «Entrar a mi
/// cuenta» con las consultas prendidas. Ningún mensaje lleva un token ni abre una sesión, y nunca atribuye una entrada al
/// dueño posterior del número.
/// </summary>
internal sealed class WhatsAppSignInReplyIssuer(IWhatsAppChannelSettings settings, IOutbox outbox, IPublicOrigin origin,
    IPlatformSettingsReader platform, WhatsAppBotReplyBuilder replies)
{
    internal async Task IssueAsync(WhatsAppInboundContext context, string destination, DateTime expiresAtUtc, bool consultations, CancellationToken ct)
    {
        if (settings.PhoneNumberId is not { } numberId) return;
        var account = context.OriginalAccount;
        string body; string? label = null; string? url = null; string? name = null; var choices = false;
        if (context.Message.OriginalUserId is not null)
        {
            if (account?.IsPlatformOperator == true) body = "Bot.OperatorNeutralReply";
            else
            {
                // La web decide si puede entrar, sin decirlo en el chat. El nombre va solo si el número sigue siendo de esa
                // cuenta y la cuenta está activa: nunca se le muestra al dueño posterior del número.
                var greets = context.OriginalMethodAvailable && context.OriginalMethod is not null
                    && account is { Status: not (UserStatus.Suspended or UserStatus.Deleted), DisplayName: not null };
                body = greets ? "Bot.EnterAccount" : "Bot.EnterAccountWithoutName";
                name = greets ? account!.DisplayName : null;
                label = "Bot.EnterAccountButton";
                url = Url("login");
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
        outbox.Enqueue("whatsapp", JsonSerializer.Serialize(reply), context.Message.OriginalUserId, reply.QueueOptions(OutboxPriority.Reply));
    }

    private string Url(string path) => new Uri(origin.Value ?? throw new InvalidOperationException("Public origin required."), path).AbsoluteUri;
}
