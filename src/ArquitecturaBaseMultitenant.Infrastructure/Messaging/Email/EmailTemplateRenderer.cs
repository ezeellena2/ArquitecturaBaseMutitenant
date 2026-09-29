using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Resources;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Renders embedded email templates with escaped replacement values.</summary>
internal sealed partial class EmailTemplateRenderer(IOptions<EmailOptions> options) : IEmailTemplateRenderer
{
    private const string LayoutTemplate = "_Layout.html";
    private const string LoginCodeTemplate = "LoginCode.html";
    private const string SignupCodeTemplate = "SignupCode.html";
    private static readonly ConcurrentDictionary<string, string> Templates = new(StringComparer.Ordinal);

    public EmailMessage RenderLoginCode(string to, string code, int lifetimeMinutes, CultureProfile culture) =>
        RenderCode(LoginCodeTemplate, to, code, lifetimeMinutes, culture);

    public EmailMessage RenderSignupCode(string to, string code, int lifetimeMinutes, CultureProfile culture) =>
        RenderCode(SignupCodeTemplate, to, code, lifetimeMinutes, culture);

    public EmailMessage RenderInvitation(string to, string loginUrl, CultureProfile culture) =>
        throw new NotSupportedException("Invitation templates are introduced with invitations.");

    public EmailMessage RenderAccountNotice(string to, AccountNotice notice, CultureProfile culture) =>
        throw new NotSupportedException("Account notice templates are introduced in a later task.");

    private EmailMessage RenderCode(string template, string to, string code, int lifetimeMinutes, CultureProfile culture)
    {
        var appName = options.Value.AppName;
        var title = NotificationTexts.Get("LoginCode.Title", culture);
        var intro = NotificationTexts.Format("LoginCode.Intro", culture, appName);
        var expiry = NotificationTexts.Format("LoginCode.Expiry", culture, lifetimeMinutes);
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
            NotificationTexts.Format("LoginCode.Subject", culture, code, appName), html, text);
    }

    private string HeaderHtml()
    {
        var settings = options.Value;
        return string.IsNullOrWhiteSpace(settings.LogoUrl)
            ? Encode(settings.AppName)
            : $"<img src=\"{Encode(settings.LogoUrl)}\" alt=\"{Encode(settings.AppName)}\" height=\"32\" style=\"display:block;border:0;height:32px;\">";
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
