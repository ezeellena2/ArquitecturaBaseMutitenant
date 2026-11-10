using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

internal sealed partial class EmailTemplateRenderer
{
    private const string ActionStyle = "display:inline-block;padding:10px 20px;border-radius:8px;background-color:#366d2a;"
        + "background-color:oklch(0.5 0.1 195);color:#fff;font-weight:bold;text-decoration:none;";

    /// <summary>
    /// Tablero Mensajes: el título, los párrafos, la grilla de datos, el botón, las notas y, en el pie, el del aviso arriba
    /// del de todos los correos. Los textos llegan traducidos y formateados: acá solo se escapan y se ubican.
    /// </summary>
    public EmailMessage RenderNotice(string to, EmailNotice notice, CultureProfile culture)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var layoutFooter = NotificationTexts.Format("Layout.Footer", culture, options.Value.AppName);
        var content = Fill("Notice.html", new Dictionary<string, string>
        {
            ["Title"] = Encode(notice.Title),
            ["Paragraphs"] = string.Concat(notice.Paragraphs.Select(value => $"<p style=\"margin:0 0 16px;\">{Encode(value)}</p>")),
            ["Details"] = notice.Details.Count == 0 ? string.Empty
                : "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" "
                    + "style=\"margin:0 0 20px;padding:10px 0;border:1px solid #e5e7eb;border-radius:8px;\">"
                    + string.Concat(notice.Details.Select(detail =>
                        $"<tr><td style=\"padding:6px 16px;width:110px;color:#6b7280;\">{Encode(detail.Label)}</td><td style=\"padding:6px 16px;color:#111827;\">{Encode(detail.Value)}</td></tr>"))
                    + "</table>",
            ["Action"] = notice.Action is not { } action ? string.Empty
                : $"<p style=\"margin:0 0 20px;\"><a href=\"{Encode(action.Url)}\" style=\"{ActionStyle}\">{Encode(action.Label)}</a></p>",
            ["Note"] = string.Concat(notice.Notes.Select(note => $"<p style=\"margin:0 0 8px;color:#6b7280;font-size:14px;\">{Encode(note)}</p>")),
        });
        var html = Fill(LayoutTemplate, new Dictionary<string, string>
        {
            ["Lang"] = Encode(culture.Entry.LanguageCode),
            ["Title"] = Encode(notice.Title),
            ["Header"] = HeaderHtml(),
            ["Content"] = content,
            ["Footer"] = notice.Footer is { Length: > 0 } footer ? Encode(footer) + "<br>" + Encode(layoutFooter) : Encode(layoutFooter),
        });
        var text = new List<string> { notice.Title };
        text.AddRange(notice.Paragraphs);
        text.AddRange(notice.Details.Select(detail => detail.Label + ": " + detail.Value));
        if (notice.Action is { } link) text.Add(link.Label + ": " + link.Url);
        text.AddRange(notice.Notes);
        if (notice.Footer is { Length: > 0 } own) text.Add(own);
        text.Add(layoutFooter);
        return new EmailMessage(to, notice.Subject, html, string.Join(Environment.NewLine + Environment.NewLine, text));
    }
}
