using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
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
    [InlineData("es-AR", "Te invitaron a", "Ver la invitación", "08/10/2026", false)]
    [InlineData("en-US", "You were invited to", "View the invitation", "10/08/2026", false)]
    [InlineData("es-AR", "Te invitaron a", "Ver la invitación", "08/10/2026", true)]
    [InlineData("en-US", "You were invited to", "View the invitation", "10/08/2026", true)]
    public async Task Invitation_renders_real_data_local_expiry_and_escapes_html(
        string cultureCode, string title, string action, string expiry, bool usesEmailCode)
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(cultureCode, Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "Mi App" }), CreateFormatter());
        var notice = new InvitationNotice("<Delta & Norte>", "Ana <Pérez>",
            new DateTime(2026, 10, 9, 1, 0, 0, DateTimeKind.Utc), "America/Argentina/Buenos_Aires",
            "https://example.test/invitacion#protected-secret", usesEmailCode);

        var message = await renderer.RenderInvitationAsync("recipient@example.test", notice, profile, Ct);

        Assert.Contains(title, message.TextBody);
        Assert.Contains(action, message.TextBody);
        Assert.Contains(expiry, message.TextBody);
        Assert.Contains("&lt;Delta &amp; Norte&gt;", message.HtmlBody);
        Assert.Contains("Ana &lt;P&#233;rez&gt;", message.HtmlBody);
        Assert.DoesNotContain("<Delta & Norte>", message.HtmlBody);
        Assert.Contains(notice.ActionUrl, message.TextBody);
        var loginNote = NotificationTexts.Get("Invitation.Note", profile);
        if (usesEmailCode)
        {
            Assert.Contains(loginNote, message.TextBody);
            Assert.Contains(System.Net.WebUtility.HtmlEncode(loginNote), message.HtmlBody);
        }
        else
        {
            Assert.DoesNotContain(loginNote, message.TextBody);
            Assert.DoesNotContain(System.Net.WebUtility.HtmlEncode(loginNote), message.HtmlBody);
        }
        Assert.DoesNotContain("protected-secret", notice.ToString());
        Assert.DoesNotContain("protected-secret", message.ToString());

        if (Environment.GetEnvironmentVariable("MT_INVITATION_EMAIL_CAPTURE_DIR") is { Length: > 0 } captureDirectory)
        {
            Directory.CreateDirectory(captureDirectory);
            var fileName = $"invitacion-{cultureCode}-{(usesEmailCode ? "con-codigo" : "sin-codigo")}.html";
            var safeHtml = message.HtmlBody.Replace("protected-secret", "captura-sintetica", StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(captureDirectory, fileName), safeHtml, Ct);
        }
    }

    // Tablero Mensajes: «correo» y no «email», y el pie completo de la invitación.
    [Fact]
    public async Task Invitation_footer_and_layout_say_correo_like_the_board()
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("es-AR", Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "ArquitecturaBase" }), CreateFormatter());
        var notice = new InvitationNotice("Grupo Delta", "Lucía Fernández", new DateTime(2026, 10, 9, 1, 0, 0, DateTimeKind.Utc),
            "America/Argentina/Buenos_Aires", "https://example.test/invitacion#protected-secret", false);

        var message = await renderer.RenderInvitationAsync("recipient@example.test", notice, profile, Ct);

        Assert.Contains("Si no esperabas este correo, podés ignorarlo. Sin el código, nadie puede entrar a tu cuenta.", message.TextBody);
        Assert.Contains("Este correo lo envió ArquitecturaBase automáticamente. No lo respondas.", message.TextBody);
        Assert.Contains(System.Net.WebUtility.HtmlEncode("Este correo lo envió ArquitecturaBase automáticamente."), message.HtmlBody);
    }

    // Tablero Mensajes, «Invitación a una organización»: Empresa, Roles y Vence; los roles de sistema en el idioma del correo.
    [Theory]
    [InlineData("es-AR", "Empresa: Delta Norte S.A.", "Roles: Administrador, Solo lectura")]
    [InlineData("en-US", "Company: Delta Norte S.A.", "Roles: Administrator, Solo lectura")]
    public async Task Invitation_lists_the_company_and_the_roles_offered(string cultureCode, string company, string roles)
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(cultureCode, Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "Mi App" }), CreateFormatter());
        var notice = new InvitationNotice("Grupo Delta", "Lucía Fernández",
            new DateTime(2026, 10, 9, 1, 0, 0, DateTimeKind.Utc), "America/Argentina/Buenos_Aires",
            "https://example.test/invitacion#protected-secret", false,
            Companies: ["Delta Norte S.A."], Roles: [SystemRoles.CompanyAdmin, "Solo lectura"]);

        var message = await renderer.RenderInvitationAsync("recipient@example.test", notice, profile, Ct);

        Assert.Contains(company, message.TextBody);
        Assert.Contains(roles, message.TextBody);
        Assert.Contains("Delta Norte S.A.", message.HtmlBody);
        Assert.DoesNotContain(SystemRoles.CompanyAdmin, message.HtmlBody);
    }

    // Tablero Mensajes, «Invitación como operador» y «… sin nombre»: sin nombre firma el equipo y no repite la app.
    [Theory]
    [InlineData("Martín Sosa", "Martín Sosa te invitó como operador de ArquitecturaBase",
        "Hola. Martín Sosa te invitó a sumarte como operador de la plataforma ArquitecturaBase.")]
    [InlineData(null, "El equipo de ArquitecturaBase te invitó como operador",
        "Hola. El equipo de ArquitecturaBase te invitó a sumarte como operador de la plataforma.")]
    public async Task Operator_invitation_matches_the_board_with_and_without_the_inviter_name(string? inviter,
        string subject, string body)
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("es-AR", Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "ArquitecturaBase" }),
            CreateFormatter());

        var message = await renderer.RenderAccountNoticeAsync("operator@example.test",
            new AccountNotice.OperatorInvited(inviter, [], new DateTime(2026, 10, 4, 15, 0, 0, DateTimeKind.Utc),
                "America/Argentina/Buenos_Aires", "https://example.test/plataforma"), profile, Ct);

        Assert.Equal(subject, message.Subject);
        Assert.Contains(body, message.TextBody);
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
        var cultures = new UserCultureResolver(new JsonReferenceDataCatalog());

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

    [Theory]
    [InlineData("es-AR", "Revisá tus métodos de ingreso",
        "Hola, Lucía. Ya no sos miembro de Delta <Sur>. Tu cuenta de ArquitecturaBase sigue activa.",
        "Si entrabas con un correo administrado por Delta <Sur>, ya no te sirve. Agregá un correo personal o tu WhatsApp para no perder tu cuenta.",
        "Revisar mis métodos")]
    [InlineData("en-US", "Review your sign-in methods",
        "Hello, Lucía. You are no longer a member of Delta <Sur>. Your ArquitecturaBase account is still active.",
        "If you signed in with an email managed by Delta <Sur>, it no longer works. Add a personal email or your WhatsApp so you don't lose your account.",
        "Review my methods")]
    public async Task Membership_end_notice_matches_the_approved_board_and_links_to_account(string cultureCode,
        string subject, string body, string managedEmail, string action)
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(cultureCode, Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "ArquitecturaBase" }));
        var notice = new AccountNotice.ReviewLoginMethods("Delta <Sur>", "https://example.test/cuenta")
        { RecipientName = "Lucía" };

        var message = await renderer.RenderAccountNoticeAsync("other-method@example.test", notice, profile, Ct);

        Assert.Equal(subject, message.Subject);
        Assert.Contains(body, message.TextBody);
        Assert.Contains(managedEmail, message.TextBody);
        Assert.Contains($"{action}: https://example.test/cuenta", message.TextBody);
        Assert.Contains("Delta &lt;Sur&gt;", message.HtmlBody);
        Assert.DoesNotContain("Delta <Sur>", message.HtmlBody);
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
    [InlineData("es-AR", "Alguien pidió recuperar tu cuenta",
        "Hola, Diego. El 27/09/2026 a las 14:35 alguien pidió recuperar tu cuenta de ArquitecturaBase y nos dio otro correo o teléfono para escribirle.",
        "Una persona de la plataforma revisa el pedido antes de cambiar algo. Si fuiste vos, no tenés que hacer nada.",
        "Si no fuiste vos, escribí a soporte@ejemplo.com.")]
    [InlineData("en-US", "Someone asked to recover your account",
        "someone asked to recover your ArquitecturaBase account and gave us another email address or phone number to contact them.",
        "A person from the platform reviews the request before changing anything. If it was you, you don't need to do anything.",
        "If it wasn't you, write to soporte@ejemplo.com.")]
    public async Task Recovery_request_notice_uses_the_approved_copy_without_button_or_new_method(string cultureCode,
        string subject, string first, string second, string note)
    {
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(cultureCode, Ct);
        var renderer = new EmailTemplateRenderer(Options.Create(new EmailOptions { AppName = "ArquitecturaBase" }),
            CreateFormatter());
        var requestedAtUtc = new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Utc);

        // Lienzo v89, Mensajes: «Recuperación: aviso a la cuenta».
        var message = await renderer.RenderAccountNoticeAsync("diego@example.test",
            new AccountNotice.RecoveryRequested(requestedAtUtc, "America/Argentina/Buenos_Aires")
            {
                RecipientName = "Diego",
                SupportEmail = Email.Create("soporte@ejemplo.com").Value,
            }, profile, Ct);

        Assert.Equal(subject, message.Subject);
        Assert.Contains(first, message.TextBody);
        Assert.Contains(second, message.TextBody);
        Assert.Contains(note, message.TextBody);
        Assert.DoesNotContain("<a href", message.HtmlBody);
        Assert.DoesNotContain("@example.test", message.TextBody);
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
