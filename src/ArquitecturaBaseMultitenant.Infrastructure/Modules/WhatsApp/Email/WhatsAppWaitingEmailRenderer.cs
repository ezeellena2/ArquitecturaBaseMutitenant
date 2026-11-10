using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Email;

/// <summary>
/// Tablero Mensajes, «Chats esperando» y «Consultas esperando»: el resumen en la cultura de quien lo recibe, con la
/// cantidad y la espera formateadas por DisplayFormatter, el botón a la bandeja y el pie que dice por qué le llega, sobre
/// el armado común de avisos del núcleo.
/// </summary>
internal sealed class WhatsAppWaitingEmailRenderer(DisplayFormatter formatter, ICultureCatalog cultures, IEmailTemplateRenderer emails)
    : IWhatsAppWaitingEmailRenderer
{
    public async Task<EmailMessage> RenderAsync(WhatsAppWaitingNotice notice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var context = await formatter.CreateAsync(notice.Culture, notice.TimeZoneId, ct);
        var profile = await new CultureProfiles(cultures).LoadAsync(context.Culture, ct);
        var prefix = notice.Platform ? "Notice.ConsultationsWaiting" : "Notice.ChatsWaiting";
        var number = notice.Waiting == 1 ? "Singular" : "Plural";
        var count = formatter.FormatDecimal(notice.Waiting, 0, context);
        var greeting = string.IsNullOrWhiteSpace(notice.RecipientName)
            ? NotificationTexts.Get("Greeting.Unnamed", profile)
            : NotificationTexts.Format("Greeting.Named", profile, notice.RecipientName);
        var body = WhatsAppTexts.Format(prefix + ".Body" + number, profile, greeting, count, notice.TeamName, notice.PlaceName,
            formatter.FormatElapsed(notice.OldestWaiting, context));
        return emails.RenderNotice(notice.Recipient, new EmailNotice(
            WhatsAppTexts.Format(prefix + ".Subject" + number, profile, count, notice.TeamName),
            WhatsAppTexts.Format(prefix + ".Title" + number, profile, count, notice.TeamName), [body])
        {
            Action = new(WhatsAppTexts.Get(prefix + ".Action", profile), notice.InboxUrl),
            Footer = WhatsAppTexts.Format(prefix + ".Footer", profile, notice.TeamName),
        }, profile);
    }
}
