using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// Comprueba textos y estructura de las plantillas de ingreso y avisos por idioma. Protege el escape HTML y
/// la ausencia de códigos en ToString.
/// </summary>
public sealed class EmailTemplateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("es-AR", "Te invitaron a", "Ver la invitación", "08/10/2026")]
    [InlineData("en-US", "You were invited to", "View the invitation", "10/08/2026")]
    public async Task Invitation_renders_real_data_local_expiry_and_escapes_html(
        string cultureCode, string title, string action, string expiry)
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(cultureCode, Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "Mi App" }), CreateFormatter());
        var notice = new InvitationNotice("<Delta & Norte>", "Ana <Pérez>",
            new DateTime(2026, 10, 9, 1, 0, 0, DateTimeKind.Utc), "America/Argentina/Buenos_Aires",
            "https://example.test/invitacion#protected-secret", false);

        var message = await renderer.RenderInvitationAsync("recipient@example.test", notice, profile, Ct);

        Assert.Contains(title, message.TextBody);
        Assert.Contains(action, message.TextBody);
        Assert.Contains(expiry, message.TextBody);
        Assert.Contains("&lt;Delta &amp; Norte&gt;", message.HtmlBody);
        Assert.Contains("Ana &lt;P&#233;rez&gt;", message.HtmlBody);
        Assert.DoesNotContain("<Delta & Norte>", message.HtmlBody);
        Assert.Contains(notice.ActionUrl, message.TextBody);
        Assert.DoesNotContain("protected-secret", notice.ToString());
        Assert.DoesNotContain("protected-secret", message.ToString());
    }

    [Theory]
    [InlineData("es-AR", "Confirmá tu correo", "es tu código para agregar este correo")]
    [InlineData("en-US", "Confirm your email", "is your code to add this email")]
    public async Task Verify_email_uses_the_approved_template_and_distinct_copy(string culture,
        string title, string subject)
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(culture, Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "ArquitecturaBase" }));
        var message = renderer.RenderVerifyEmailCode("user@example.test", "715204", 10, profile);
        Assert.Contains(title, message.TextBody);
        Assert.Contains(subject, message.Subject);
        Assert.Contains("715204", message.TextBody);
    }

    [Theory]
    [InlineData("es-AR", "Tu código de acceso", "Usá este código")]
    [InlineData("en-US", "Your access code", "Use this code")]
    public async Task Login_and_signup_codes_render_the_approved_copy_in_the_selected_language(
        string culture, string title, string intro)
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(culture, Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "Mi App" }));

        var login = renderer.RenderLoginCode("user@example.test", "123456", 10, profile);
        var signup = renderer.RenderSignupCode("user@example.test", "654321", 10, profile);

        Assert.Contains(title, login.TextBody);
        Assert.Contains(intro, login.TextBody);
        Assert.Contains("123456", login.Subject);
        Assert.Contains("654321", signup.Subject);
        Assert.Contains(title, signup.TextBody);
        Assert.Contains("654321", signup.TextBody);
        Assert.Contains($"<html lang=\"{culture[..2]}\">", login.HtmlBody);
    }

    [Fact]
    public async Task Code_is_html_escaped_and_never_returned_by_ToString()
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("es-AR", Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "Mi App" }));

        var message = renderer.RenderLoginCode("user@example.test", "<123&456>", 10, profile);

        Assert.Contains("&lt;123&amp;456&gt;", message.HtmlBody);
        Assert.DoesNotContain("<123&456>", message.HtmlBody);
        Assert.DoesNotContain("123", message.ToString());
    }

    [Fact]
    public async Task Notifications_follow_the_enabled_catalog_fallback_chain()
    {
        var catalog = new FallbackCatalog();
        var profile = await new CultureProfiles(catalog).LoadAsync("it-IT", Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "Mi App" }));

        var message = renderer.RenderLoginCode("user@example.test", "123456", 10, profile);

        Assert.Contains("Your access code", message.HtmlBody);
        Assert.Equal("Your access code", NotificationTexts.Get("LoginCode.Title", profile));
    }

    [Fact]
    public async Task User_culture_prefers_enabled_account_then_organization_then_catalog_default()
    {
        var cultures = new UserCultures(new JsonReferenceDataCatalog());

        Assert.Equal("en-US", (await cultures.ResolveAsync("en-US", "es-AR", Ct)).Entry.Code);
        Assert.Equal("en-US", (await cultures.ResolveAsync("de-DE", "en-US", Ct)).Entry.Code);
        Assert.Equal("es-AR", (await cultures.ResolveAsync(null, "de-DE", Ct)).Entry.Code);
    }

    [Fact]
    public async Task Account_notices_render_the_four_approved_email_templates_with_local_dates()
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("es-AR", Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "ArquitecturaBase" }),
            CreateFormatter());
        const string zone = "America/Argentina/Buenos_Aires";
        var occurred = new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Utc);
        var scheduled = new DateTime(2026, 10, 27, 17, 35, 0, DateTimeKind.Utc);
        const string url = "https://example.test/login";

        var changed = await renderer.RenderAccountNoticeAsync("user@example.test",
            new AccountNotice.LoginMethodChanged("Added", LoginMethodType.Phone, "+54 9 11 •••• 4521",
                occurred, zone, url) { RecipientName = "Lucía" }, profile, Ct);
        var requested = await renderer.RenderAccountNoticeAsync("user@example.test",
            new AccountNotice.DeletionRequested(occurred, scheduled, zone, url)
            { RecipientName = "Diego" }, profile, Ct);
        var cancelled = await renderer.RenderAccountNoticeAsync("user@example.test",
            new AccountNotice.DeletionCancelled(occurred, zone, url)
            { RecipientName = "Diego" }, profile, Ct);
        var deleted = await renderer.RenderAccountNoticeAsync("user@example.test",
            new AccountNotice.AccountDeleted { RecipientName = "Diego" }, profile, Ct);

        Assert.Equal("Agregaste un WhatsApp a tu cuenta", changed.Subject);
        Assert.Contains("Hola, Lucía. El 27/09/2026 a las 14:35 agregaste el WhatsApp", changed.TextBody);
        Assert.Contains("+54 9 11 •••• 4521", changed.TextBody);
        Assert.Contains(url, changed.HtmlBody);
        Assert.Equal("Pediste la baja de tu cuenta", requested.Subject);
        Assert.Contains("Tu cuenta se elimina el 27/10/2026.", requested.TextBody);
        Assert.Contains("Cancelaste la baja de tu cuenta", cancelled.Subject);
        Assert.Contains("El 27/09/2026 a las 14:35 cancelaste la baja", cancelled.TextBody);
        Assert.Equal("Tu cuenta fue eliminada", deleted.Subject);
        Assert.Contains("Este correo ya no está asociado a ninguna cuenta.", deleted.TextBody);
    }

    [Fact]
    public async Task Login_method_change_uses_the_three_approved_variants_and_escapes_personal_data()
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("es-AR", Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "ArquitecturaBase" }),
            CreateFormatter());
        var occurred = new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Utc);
        const string zone = "America/Argentina/Buenos_Aires";
        const string url = "https://example.test/me?x=1&y=2";

        var removed = await renderer.RenderAccountNoticeAsync("user@example.test",
            new AccountNotice.LoginMethodChanged("Removed", LoginMethodType.Email, "l***@delta.ejemplo.com",
                occurred, zone, url) { RecipientName = "Lucía" }, profile, Ct);
        var primary = await renderer.RenderAccountNoticeAsync("user@example.test",
            new AccountNotice.LoginMethodChanged("Primary", LoginMethodType.Email, "l***@gmail.com",
                occurred, zone, url) { RecipientName = "Lucía" }, profile, Ct);

        Assert.Equal("Quitaste un correo de tu cuenta", removed.Subject);
        Assert.Contains("Ya no sirve para ingresar.", removed.TextBody);
        Assert.Equal("Cambiaste el método principal de tu cuenta", primary.Subject);
        Assert.Contains("Desde ahora, los avisos llegan ahí.", primary.TextBody);
        Assert.Contains("x=1&amp;y=2", removed.HtmlBody);
        Assert.DoesNotContain("x=1&y=2", removed.HtmlBody);
    }

    [Fact]
    public async Task Account_notices_translate_copy_and_local_format_to_english()
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("en-US", Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "ArquitecturaBase" }),
            CreateFormatter());
        var occurred = new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Utc);

        var message = await renderer.RenderAccountNoticeAsync("user@example.test",
            new AccountNotice.DeletionCancelled(occurred, "America/Argentina/Buenos_Aires", "https://example.test/me")
            { RecipientName = "Diego" }, profile, Ct);

        Assert.Equal("You cancelled account deletion", message.Subject);
        Assert.Contains("Hello, Diego.", message.TextBody);
        Assert.Contains("at 2:35", message.TextBody);
        Assert.Contains("Go to My Account", message.TextBody);
    }

    [Theory]
    [InlineData("es-AR", "Hola.", "Hola, Ana Pérez.")]
    [InlineData("en-US", "Hello.", "Hello, Ana Pérez.")]
    public async Task Account_notice_uses_full_name_or_approved_anonymous_greeting(
        string cultureCode, string unnamedGreeting, string namedGreeting)
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(cultureCode, Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "ArquitecturaBase" }),
            CreateFormatter());

        var unnamed = await renderer.RenderAccountNoticeAsync("user@example.test", new AccountNotice.AccountDeleted(),
            profile, Ct);
        var named = await renderer.RenderAccountNoticeAsync("user@example.test",
            new AccountNotice.AccountDeleted { RecipientName = "Ana Pérez" }, profile, Ct);

        Assert.Contains(unnamedGreeting, unnamed.TextBody);
        Assert.Contains(namedGreeting, named.TextBody);
    }

    private static DisplayFormatter CreateFormatter()
    {
        var catalog = new JsonReferenceDataCatalog();
        return new DisplayFormatter(catalog, catalog, catalog, catalog, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(TimeProvider.System), TimeProvider.System);
    }

    private sealed class FallbackCatalog : ICultureCatalog
    {
        private readonly ICultureCatalog _source = new JsonReferenceDataCatalog();

        public async Task<IReadOnlyList<CultureCatalogEntry>> ListAsync(CancellationToken cancellationToken)
        {
            var rows = await _source.ListAsync(cancellationToken);
            return [.. rows, rows.Single(row => row.Code == "en-US") with
            {
                Code = "it-IT", LanguageCode = "it", CountryCode = "IT",
                FallbackCulture = "en-US", IsDefault = false,
            }];
        }

        public async Task<CultureCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken) =>
            (await ListAsync(cancellationToken)).FirstOrDefault(row =>
                string.Equals(row.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}
