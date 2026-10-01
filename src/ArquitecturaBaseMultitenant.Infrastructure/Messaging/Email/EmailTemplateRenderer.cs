using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Combina plantillas HTML embebidas con textos localizados para producir correos de ingreso y avisos. Escapa los valores variables antes de insertarlos en el HTML.</summary>
internal sealed partial class EmailTemplateRenderer(IOptions<EmailOptions> options) : IEmailTemplateRenderer
{
    private DisplayFormatter? _formatter;
    public EmailTemplateRenderer(IOptions<EmailOptions> options, DisplayFormatter formatter) : this(options) =>
        _formatter = formatter;
    private const string LayoutTemplate = "_Layout.html";
    private const string LoginCodeTemplate = "LoginCode.html";
    private const string SignupCodeTemplate = "SignupCode.html";
    private static readonly ConcurrentDictionary<string, string> Templates = new(StringComparer.Ordinal);

    public EmailMessage RenderLoginCode(string to, string code, int lifetimeMinutes, CultureProfile culture) =>
        RenderCode(LoginCodeTemplate, to, code, lifetimeMinutes, culture);

    public EmailMessage RenderSignupCode(string to, string code, int lifetimeMinutes, CultureProfile culture) =>
        RenderCode(SignupCodeTemplate, to, code, lifetimeMinutes, culture);

    public EmailMessage RenderVerifyEmailCode(string to, string code, int lifetimeMinutes, CultureProfile culture) =>
        RenderCode(LoginCodeTemplate, to, code, lifetimeMinutes, culture, "VerifyEmail");

    public async Task<EmailMessage> RenderAccountNoticeAsync(string to, AccountNotice notice, CultureProfile culture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var greeting = string.IsNullOrWhiteSpace(notice.RecipientName)
            ? NotificationTexts.Get("Greeting.Unnamed", culture)
            : NotificationTexts.Format("Greeting.Named", culture, notice.RecipientName);

        var appName = options.Value.AppName;
        var (key, template, paragraphs, actionLabel, actionUrl, note) = notice switch
        {
            AccountNotice.LoginMethodChanged changed => await MethodChangedAsync(changed, greeting, appName, culture, cancellationToken),
            AccountNotice.DeletionRequested requested => await DeletionRequestedAsync(requested, greeting, appName, culture, cancellationToken),
            AccountNotice.DeletionCancelled cancelled => await DeletionCancelledAsync(cancelled, greeting, appName, culture, cancellationToken),
            AccountNotice.AccountDeleted => ("AccountDeleted", "AccountDeleted.html",
                new[] { NotificationTexts.Format("AccountDeleted.Body", culture, greeting, appName),
                    NotificationTexts.Get("AccountDeleted.After", culture) }, string.Empty, string.Empty, string.Empty),
            _ => throw new NotSupportedException("This account notice template belongs to a later stage."),
        };
        var title = NotificationTexts.Get(key + ".Title", culture);
        var subject = NotificationTexts.Get(key + ".Subject", culture);
        var htmlContent = Fill(template, new Dictionary<string, string>
        {
            ["Title"] = Encode(title),
            ["Paragraphs"] = string.Join(string.Empty, paragraphs.Select(value => $"<p style=\"margin:0 0 16px;\">{Encode(value)}</p>")),
            ["Action"] = actionLabel.Length == 0 ? string.Empty :
                $"<p style=\"margin:0 0 20px;\"><a href=\"{Encode(actionUrl)}\" style=\"display:inline-block;padding:10px 20px;border-radius:8px;background-color:#007475;background-color:oklch(0.5 0.1 195);color:#fff;font-weight:bold;text-decoration:none;\">{Encode(actionLabel)}</a></p>",
            ["Note"] = note.Length == 0 ? string.Empty : $"<p style=\"margin:0;color:#6b7280;font-size:14px;\">{Encode(note)}</p>",
        });
        var html = Fill(LayoutTemplate, new Dictionary<string, string>
        {
            ["Lang"] = Encode(culture.Entry.LanguageCode),
            ["Title"] = Encode(title),
            ["Header"] = HeaderHtml(),
            ["Content"] = htmlContent,
            ["Footer"] = Encode(NotificationTexts.Format("Layout.Footer", culture, appName)),
        });
        var textParts = new List<string> { title };
        textParts.AddRange(paragraphs);
        if (actionLabel.Length > 0) textParts.Add($"{actionLabel}: {actionUrl}");
        if (note.Length > 0) textParts.Add(note);
        textParts.Add(NotificationTexts.Format("Layout.Footer", culture, appName));
        return new EmailMessage(to, subject, html, string.Join(Environment.NewLine + Environment.NewLine, textParts));
    }

    private async Task<(string Key, string Template, string[] Paragraphs, string Action, string Url, string Note)>
        MethodChangedAsync(AccountNotice.LoginMethodChanged notice, string name, string appName,
            CultureProfile culture, CancellationToken ct)
    {
        var formatter = _formatter ?? throw new InvalidOperationException("DisplayFormatter is required for account notices.");
        var context = await formatter.CreateAsync(culture.Entry.Code, notice.TimeZoneId, ct);
        var date = formatter.FormatDate(notice.OccurredAtUtc, context);
        var time = formatter.FormatTime(notice.OccurredAtUtc, context);
        var key = (notice.Change, notice.MethodType) switch
        {
            ("Added", LoginMethodType.Phone) => "LoginMethodAddedPhone",
            ("Added", LoginMethodType.Email) => "LoginMethodAddedEmail",
            ("Added", LoginMethodType.Google) => "LoginMethodAddedGoogle",
            ("Removed", LoginMethodType.Email) => "LoginMethodRemovedEmail",
            ("Removed", LoginMethodType.Google) => "LoginMethodRemovedGoogle",
            ("Primary", LoginMethodType.Email) => "LoginMethodPrimaryEmail",
            ("Primary", LoginMethodType.Google) => "LoginMethodPrimaryEmail",
            _ => throw new NotSupportedException("This login method notice has no approved copy."),
        };
        return (key, "LoginMethodChanged.html",
            [NotificationTexts.Format(key + ".Body", culture, name, date, time, notice.MaskedMethod, appName)],
            NotificationTexts.Get("Action.MyAccount", culture), notice.ActionUrl,
            NotificationTexts.Get("Notice.NotMe", culture));
    }

    private async Task<(string Key, string Template, string[] Paragraphs, string Action, string Url, string Note)>
        DeletionRequestedAsync(AccountNotice.DeletionRequested notice, string name, string appName,
            CultureProfile culture, CancellationToken ct)
    {
        var formatter = _formatter ?? throw new InvalidOperationException("DisplayFormatter is required for account notices.");
        var context = await formatter.CreateAsync(culture.Entry.Code, notice.TimeZoneId, ct);
        var date = formatter.FormatDate(notice.RequestedAtUtc, context);
        var time = formatter.FormatTime(notice.RequestedAtUtc, context);
        var scheduled = formatter.FormatDate(notice.ScheduledForUtc, context);
        return ("DeletionRequested", "AccountDeletionRequested.html",
            [NotificationTexts.Format("DeletionRequested.Body", culture, name, date, time, appName),
                NotificationTexts.Format("DeletionRequested.Scheduled", culture, scheduled)],
            NotificationTexts.Get("Action.Login", culture), notice.ActionUrl,
            NotificationTexts.Get("DeletionRequested.Note", culture));
    }

    private async Task<(string Key, string Template, string[] Paragraphs, string Action, string Url, string Note)>
        DeletionCancelledAsync(AccountNotice.DeletionCancelled notice, string name, string appName,
            CultureProfile culture, CancellationToken ct)
    {
        var formatter = _formatter ?? throw new InvalidOperationException("DisplayFormatter is required for account notices.");
        var context = await formatter.CreateAsync(culture.Entry.Code, notice.TimeZoneId, ct);
        var date = formatter.FormatDate(notice.OccurredAtUtc, context);
        var time = formatter.FormatTime(notice.OccurredAtUtc, context);
        return ("DeletionCancelled", "AccountDeletionCancelled.html",
            [NotificationTexts.Format("DeletionCancelled.Body", culture, name, date, time, appName)],
            NotificationTexts.Get("Action.MyAccount", culture), notice.ActionUrl,
            NotificationTexts.Get("Notice.NotMe", culture));
    }

    private EmailMessage RenderCode(string template, string to, string code, int lifetimeMinutes, CultureProfile culture,
        string key = "LoginCode")
    {
        var appName = options.Value.AppName;
        var title = NotificationTexts.Get(key + ".Title", culture);
        var intro = NotificationTexts.Format(key + ".Intro", culture, appName);
        var expiry = NotificationTexts.Format(key + ".Expiry", culture, lifetimeMinutes);
        var footer = NotificationTexts.Format("Layout.Footer", culture, appName);

        var content = Fill(template, new Dictionary<string, string>
        {
            ["Title"] = Encode(title),
            ["Intro"] = Encode(intro),
            ["Code"] = Encode(code),
            ["Expiry"] = Encode(expiry),
        });
        var html = Fill(LayoutTemplate, new Dictionary<string, string>
        {
            ["Lang"] = Encode(culture.Entry.LanguageCode),
            ["Title"] = Encode(title),
            ["Header"] = HeaderHtml(),
            ["Content"] = content,
            ["Footer"] = Encode(footer),
        });
        var paragraphBreak = Environment.NewLine + Environment.NewLine;
        var text = string.Join(paragraphBreak, title, intro, code, expiry, footer);
        return new EmailMessage(to,
            NotificationTexts.Format(key + ".Subject", culture, code, appName), html, text);
    }

    private string HeaderHtml()
    {
        var settings = options.Value;
        return string.IsNullOrWhiteSpace(settings.LogoUrl)
            ? Encode(settings.AppName)
            : $"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr><td style=\"padding-right:10px;\"><img src=\"{Encode(settings.LogoUrl)}\" alt=\"\" height=\"24\" width=\"24\" style=\"display:block;border:0;height:24px;width:24px;border-radius:7px;\"></td><td style=\"font-size:16px;font-weight:bold;color:#111827;\">{Encode(settings.AppName)}</td></tr></table>";
    }

    private static string Fill(string templateName, Dictionary<string, string> values) =>
        PlaceholderPattern().Replace(Load(templateName), match =>
            values.TryGetValue(match.Groups["name"].Value, out var value)
                ? value
                : throw new InvalidOperationException($"Template '{templateName}' has no value for '{match.Value}'."));

    private static string Load(string templateName) =>
        Templates.GetOrAdd(templateName, static name =>
        {
            using var stream = typeof(EmailTemplateRenderer).Assembly.GetManifestResourceStream("EmailTemplates." + name)
                ?? throw new InvalidOperationException($"Missing email template '{name}'.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    [GeneratedRegex(@"\{\{(?<name>[A-Za-z]+)\}\}")]
    private static partial Regex PlaceholderPattern();
}
