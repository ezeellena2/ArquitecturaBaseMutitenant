using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
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
        var details = new List<(string Label, string Value)>();
        if (notice.Companies is { Count: > 0 } companies)
            details.Add((NotificationTexts.Get("Invitation.CompanyLabel", culture), string.Join(", ", companies)));
        if (notice.Roles is { Count: > 0 } roles)
            details.Add((NotificationTexts.Get("Invitation.RolesLabel", culture),
                string.Join(", ", roles.Select(role => RoleTexts.NameIn(role, culture.Culture)).Distinct(StringComparer.Ordinal))));
        details.Add((NotificationTexts.Get("Invitation.ExpiresLabel", culture), expires));
        return ComposeInvitation(to, culture, subject, title, body, details,
            NotificationTexts.Get("Invitation.Action", culture), notice.ActionUrl,
            notice.UsesEmailCode ? [NotificationTexts.Get("Invitation.Note", culture)] : []);
    }

    /// <summary>
    /// Lienzo v90, Mensajes › «Invitación como operador»: quién invita (sin nombre, el equipo de la plataforma), los
    /// permisos por nombre, el vencimiento en la zona de la cuenta invitada, «Ingresar» y las dos notas.
    /// </summary>
    private async Task<EmailMessage> RenderOperatorInvitedAsync(string to, AccountNotice.OperatorInvited notice,
        CultureProfile culture, CancellationToken cancellationToken)
    {
        var formatter = _formatter ?? throw new InvalidOperationException("DisplayFormatter is required for invitations.");
        var context = await formatter.CreateAsync(culture.Entry.Code, notice.TimeZoneId, cancellationToken);
        var appName = options.Value.AppName;
        var greeting = NotificationTexts.Get("Greeting.Unnamed", culture);
        // Sin nombre firma «El equipo de {app}», y el asunto y el texto no repiten la app (Mensajes › «… sin nombre»).
        var team = NotificationTexts.Format("OperatorInvited.UnnamedInviter", culture, appName);
        var (subject, body) = string.IsNullOrWhiteSpace(notice.InviterName)
            ? (NotificationTexts.Format("OperatorInvited.UnnamedSubject", culture, team),
                NotificationTexts.Format("OperatorInvited.UnnamedBody", culture, greeting, team))
            : (NotificationTexts.Format("OperatorInvited.Subject", culture, notice.InviterName, appName),
                NotificationTexts.Format("OperatorInvited.Body", culture, greeting, notice.InviterName, appName));
        var permissions = notice.Permissions
            .Select(permission => PermissionTexts.PermissionIn(permission, culture.Culture)).ToArray();
        return ComposeInvitation(to, culture, subject,
            NotificationTexts.Get("OperatorInvited.Title", culture), body,
            [
                (NotificationTexts.Get("OperatorInvited.PermissionsLabel", culture), JoinList(permissions, culture)),
                (NotificationTexts.Get("Invitation.ExpiresLabel", culture), formatter.FormatDate(notice.ExpiresAtUtc, context)),
            ],
            NotificationTexts.Get("Action.Login", culture), notice.ActionUrl,
            [NotificationTexts.Get("OperatorInvited.Note", culture), NotificationTexts.Get("OperatorInvited.PlatformOnly", culture)]);
    }

    // «A, B y C»: la conjunción del último elemento sale de los textos del idioma del aviso.
    private static string JoinList(string[] items, CultureProfile culture) => items.Length switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => NotificationTexts.Format("List.And", culture, string.Join(", ", items.Take(items.Length - 1)), items[^1]),
    };

    // Tablero Mensajes: las invitaciones llevan sus datos en una grilla, el botón, sus notas y, debajo, el aviso de la
    // invitación y el pie de todos los correos.
    private EmailMessage ComposeInvitation(string to, CultureProfile culture, string subject, string title, string body,
        IReadOnlyList<(string Label, string Value)> details, string action, string actionUrl, IReadOnlyList<string> notes)
    {
        var footer = NotificationTexts.Get("Invitation.Footer", culture);
        var layoutFooter = NotificationTexts.Format("Layout.Footer", culture, options.Value.AppName);
        var content = Fill("Invitation.html", new Dictionary<string, string>
        {
            ["Title"] = Encode(title), ["Body"] = Encode(body),
            ["Details"] = string.Concat(details.Select(detail =>
                $"<tr><td style=\"padding:6px 16px;width:110px;color:#6b7280;\">{Encode(detail.Label)}</td><td style=\"padding:6px 16px;color:#111827;\">{Encode(detail.Value)}</td></tr>")),
            ["ActionLabel"] = Encode(action), ["ActionUrl"] = Encode(actionUrl),
            ["Note"] = string.Concat(notes.Select((note, index) =>
                $"<p style=\"margin:{(index == 0 ? 20 : 8)}px 0 0;color:#6b7280;font-size:14px;\">{Encode(note)}</p>")),
        });
        var html = Fill(LayoutTemplate, new Dictionary<string, string>
        {
            ["Lang"] = Encode(culture.Entry.LanguageCode), ["Title"] = Encode(title), ["Header"] = HeaderHtml(),
            ["Content"] = content, ["Footer"] = Encode(footer) + "<br>" + Encode(layoutFooter),
        });
        var text = new List<string> { title, body };
        text.AddRange(details.Select(detail => detail.Label + ": " + detail.Value));
        text.Add(action + ": " + actionUrl);
        text.AddRange(notes);
        text.Add(footer);
        text.Add(layoutFooter);
        return new EmailMessage(to, subject, html, string.Join(Environment.NewLine + Environment.NewLine, text));
    }
}
