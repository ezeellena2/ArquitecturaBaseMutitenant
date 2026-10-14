using System.Net;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Email;

/// <summary>El correo «Chats esperando» (o «Consultas esperando»): el equipo, la cantidad formateada y el enlace a la bandeja.</summary>
internal sealed class WhatsAppWaitingEmailRenderer(DisplayFormatter formatter, ICultureCatalog cultures) : IWhatsAppWaitingEmailRenderer
{
    private static readonly string Template = ReadTemplate();

    public async Task<EmailMessage> RenderAsync(string recipient, string teamName, int waiting, string inboxUrl, bool platform, string culture,
        string zone, CancellationToken ct)
    {
        var context = await formatter.CreateAsync(culture, zone, ct);
        var profile = await new CultureProfiles(cultures).LoadAsync(culture, ct);
        var prefix = platform ? "Notice.ConsultationsWaiting" : "Notice.ChatsWaiting";
        var subject = WhatsAppTexts.Get(prefix + ".Subject", profile).Replace("{0}", teamName, StringComparison.Ordinal);
        var body = WhatsAppTexts.Get(prefix + ".Body", profile).Replace("{0}", teamName, StringComparison.Ordinal)
            .Replace("{1}", formatter.FormatDecimal(waiting, 0, context), StringComparison.Ordinal);
        var action = WhatsAppTexts.Get(prefix + ".Action", profile);
        var language = System.Globalization.CultureInfo.GetCultureInfo(context.Culture).TwoLetterISOLanguageName;
        var html = Template.Replace("{{Language}}", WebUtility.HtmlEncode(language), StringComparison.Ordinal)
            .Replace("{{Subject}}", WebUtility.HtmlEncode(subject), StringComparison.Ordinal)
            .Replace("{{Body}}", WebUtility.HtmlEncode(body), StringComparison.Ordinal)
            .Replace("{{Url}}", WebUtility.HtmlEncode(inboxUrl), StringComparison.Ordinal)
            .Replace("{{Action}}", WebUtility.HtmlEncode(action), StringComparison.Ordinal);
        return new(recipient, subject, html, body + Environment.NewLine + inboxUrl);
    }

    private static string ReadTemplate()
    {
        using var stream = typeof(WhatsAppWaitingEmailRenderer).Assembly.GetManifestResourceStream("WhatsAppEmail.ChatsWaiting.html")
            ?? throw new InvalidOperationException("The installed WhatsApp email template is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
