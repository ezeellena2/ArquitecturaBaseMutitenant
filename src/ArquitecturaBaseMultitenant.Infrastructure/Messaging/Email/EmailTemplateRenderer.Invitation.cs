using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

internal sealed partial class EmailTemplateRenderer
{
    public async Task<EmailMessage> RenderInvitationAsync(string to, InvitationNotice notice, CultureProfile culture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var formatter = _formatter ?? throw new InvalidOperationException("DisplayFormatter is required for invitations.");
        var context = await formatter.CreateAsync(culture.Entry.Code, notice.TimeZoneId, cancellationToken);
        var expires = formatter.FormatDate(notice.ExpiresAtUtc, context);
        var title = NotificationTexts.Format("Invitation.Title", culture, notice.OrganizationName);
        var subject = NotificationTexts.Format("Invitation.Subject", culture, notice.InviterName, notice.OrganizationName);
        var body = NotificationTexts.Format("Invitation.Body", culture,
            NotificationTexts.Get("Greeting.Unnamed", culture), notice.InviterName, notice.OrganizationName, options.Value.AppName);
        var action = NotificationTexts.Get("Invitation.Action", culture);
        var expiryLabel = NotificationTexts.Get("Invitation.ExpiresLabel", culture);
        var note = notice.UsesEmailCode ? NotificationTexts.Get("Invitation.Note", culture) : string.Empty;
        var footer = NotificationTexts.Get("Invitation.Footer", culture);
        var content = Fill("Invitation.html", new Dictionary<string, string>
        {
            ["Title"] = Encode(title), ["Body"] = Encode(body), ["ExpiryLabel"] = Encode(expiryLabel),
            ["Expiry"] = Encode(expires), ["ActionLabel"] = Encode(action), ["ActionUrl"] = Encode(notice.ActionUrl),
            ["Note"] = note.Length == 0 ? string.Empty : $"<p style=\"margin:20px 0 0;color:#6b7280;font-size:14px;\">{Encode(note)}</p>",
        });
        var html = Fill(LayoutTemplate, new Dictionary<string, string>
        {
            ["Lang"] = Encode(culture.Entry.LanguageCode), ["Title"] = Encode(title), ["Header"] = HeaderHtml(),
            ["Content"] = content, ["Footer"] = Encode(footer),
        });
        var text = new List<string> { title, body, expiryLabel + ": " + expires, action + ": " + notice.ActionUrl };
        if (note.Length > 0) text.Add(note);
        text.Add(footer);
        return new EmailMessage(to, subject, html, string.Join(Environment.NewLine + Environment.NewLine, text));
    }
}
