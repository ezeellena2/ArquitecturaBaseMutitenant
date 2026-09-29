using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

public sealed class EmailTemplateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
    public void Email_registration_exposes_the_template_renderer_port()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing",
        });
        builder.Services.AddEmail(builder.Configuration, builder.Environment);
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        Assert.IsType<EmailTemplateRenderer>(scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>());
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
