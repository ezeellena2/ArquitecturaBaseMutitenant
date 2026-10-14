using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.Options;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
public sealed class WhatsAppTemplateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Phone = "+5491134567890";
    private const string Zone = "America/Argentina/Buenos_Aires";
    private static readonly DateTime Instant = new(2026, 10, 9, 1, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData("es-AR", "es", "08/10/2026", "agregaste")]
    [InlineData("en-US", "en", "10/08/2026", "added")]
    public async Task All_sixteen_names_select_language_order_and_buttons(string cultureCode, string language, string date, string added)
    {
        var culture = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(cultureCode, Ct);
        var renderer = Renderer();
        var catalog = new WhatsAppTemplateCatalog(Options.Create(new WhatsAppOptions()));
        Assert.Equal(16, catalog.Definitions.Count);
        var expectedKeys = new[] { "LoginCode", "Invitation", "LoginMethodChanged", "ReviewLoginMethods", "DeletionRequested",
            "DeletionRequestedByPlatform", "DeletionRequestedSuspended", "DeletionCancelled", "AccountDeleted", "RecoveryReceived",
            "RecoveryApproved", "RecoveryRejected", "RecoveryRequested", "OrganizationApproved", "OrganizationRejected", "PublicPageModerated" };
        Assert.Equal(expectedKeys.Order(StringComparer.Ordinal), catalog.Definitions.Select(value => value.Key).Order(StringComparer.Ordinal));
        Assert.Equal(expectedKeys.Order(StringComparer.Ordinal), typeof(WhatsAppTemplateOptions).GetProperties().Select(value => value.Name).Order(StringComparer.Ordinal));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "ArquitecturaBaseMultitenant.slnx"))) root = root.Parent ?? throw new InvalidOperationException("Solution root missing.");
        using var configured = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, "src", "ArquitecturaBaseMultitenant.Api", "appsettings.json"), Ct));
        Assert.Equal(expectedKeys.Order(StringComparer.Ordinal), configured.RootElement.GetProperty("WhatsApp").GetProperty("Templates")
            .EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal));
        Assert.Equal(16, catalog.Definitions.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count());
        var messages = new List<WhatsAppOutboundMessage>
        {
            renderer.RenderCode(Phone, "483920", 10, culture),
            await renderer.RenderInvitationAsync(Phone, new InvitationNotice("Delta", "Lucía", Instant, Zone,
                "https://example.test/invitacion#protected-secret", true), culture, Ct),
        };
        foreach (var notice in Notices()) messages.Add((await renderer.RenderNoticeAsync(Phone, notice, culture, Ct))!);
        Assert.Equal(16, messages.Count);
        foreach (var message in messages)
        {
            var definition = Assert.Single(catalog.Definitions, value => value.Name == message.TemplateName);
            Assert.Equal(language, message.Language);
            Assert.Equal(definition.BodyParameters, message.BodyParameters.Count);
            Assert.Equal(definition.HasUrlButton || definition.IsAuthentication, message.ButtonParameter is not null);
            var body = WhatsAppTemplateCatalog.Body(definition.Key, culture);
            for (var index = 1; index <= definition.BodyParameters; index++) Assert.Contains("{{" + index + "}}", body);
            Assert.DoesNotContain("{{" + (definition.BodyParameters + 1) + "}}", body);
            using var payload = JsonDocument.Parse(JsonSerializer.Serialize(WhatsAppCloudClient.BuildPayload(message)));
            Assert.Equal("5491134567890", payload.RootElement.GetProperty("to").GetString());
            Assert.Equal(message.TemplateName, payload.RootElement.GetProperty("template").GetProperty("name").GetString());
            Assert.DoesNotContain(Phone, message.ToString());
            Assert.DoesNotContain("483920", message.ToString());
            Assert.DoesNotContain("protected-secret", message.ToString());
        }
        Assert.Equal(["Lucía", "Delta", date], messages[1].BodyParameters);
        Assert.Equal("invitacion#protected-secret", messages[1].ButtonParameter);
        Assert.Equal(added, messages[2].BodyParameters[0]);
        Assert.Contains(date, messages[2].BodyParameters[2]);
        Assert.Equal("login", Assert.Single(messages, value => value.TemplateName == "recuperacion_aprobada").ButtonParameter);
        Assert.Equal([date, WhatsAppTexts.Get("Support.Contact", culture)], Assert.Single(messages, value => value.TemplateName == "baja_cuenta_plataforma_suspendida").BodyParameters);
        Assert.Equal([WhatsAppTexts.Get("Support.Contact", culture)], Assert.Single(messages, value => value.TemplateName == "recuperacion_aviso_cuenta").BodyParameters);
        foreach (var change in new[] { LoginMethodChange.Added, LoginMethodChange.Removed, LoginMethodChange.Primary })
        {
            var changed = await renderer.RenderNoticeAsync(Phone, new AccountNotice.LoginMethodChanged(change, LoginMethodType.Phone, "•••• 4521", Instant, Zone, "https://example.test/me"), culture, Ct);
            Assert.Equal(WhatsAppTexts.Get("MethodChange." + change, culture), changed!.BodyParameters[0]);
            Assert.Equal("•••• 4521", changed.BodyParameters[1]);
            Assert.Equal(language == "es" ? "08/10/2026 a las 22:00" : "10/08/2026 at 10:00 PM", changed.BodyParameters[2]);
        }
        var allowed = await renderer.RenderNoticeAsync(Phone, new AccountNotice.PublicPageModerated("Delta", false), culture, Ct);
        Assert.Equal(["Delta", WhatsAppTexts.Get("Page.Allowed", culture)], allowed!.BodyParameters);
        Assert.Null(allowed.ButtonParameter);
        Assert.Null(await renderer.RenderNoticeAsync(Phone, new AccountNotice.OperatorInvited(null, [], Instant, Zone, "https://example.test/platform"), culture, Ct));
    }
    [Fact]
    public async Task Authentication_uses_same_code_in_body_and_copy_button_and_only_transient_payload()
    {
        var culture = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("es-AR", Ct);
        var message = Renderer().RenderCode(Phone, "483920", 10, culture);
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(WhatsAppCloudClient.BuildPayload(message)));
        var components = payload.RootElement.GetProperty("template").GetProperty("components");
        Assert.Equal(2, components.GetArrayLength());
        Assert.Equal("483920", components[0].GetProperty("parameters")[0].GetProperty("text").GetString());
        Assert.Equal("483920", components[1].GetProperty("parameters")[0].GetProperty("text").GetString());
        Assert.Equal("url", components[1].GetProperty("sub_type").GetString());
        Assert.Throws<InvalidOperationException>(() => Renderer().RenderCode(Phone, "483920", 5, culture));
        await Assert.ThrowsAsync<ArgumentException>(() => Renderer().RenderNoticeAsync(Phone, new AccountNotice.RecoveryApproved("https://foreign.test/login"), culture, Ct));
    }
    private static IEnumerable<AccountNotice> Notices()
    {
        yield return new AccountNotice.LoginMethodChanged(LoginMethodChange.Added, LoginMethodType.Phone, "•••• 4521", Instant, Zone, "https://example.test/me");
        yield return new AccountNotice.ReviewLoginMethods("Delta", "https://example.test/me");
        yield return new AccountNotice.DeletionRequested(Instant, Instant, Zone, "https://example.test/me");
        yield return new AccountNotice.DeletionCancelled(Instant, Zone, "https://example.test/me");
        yield return new AccountNotice.AccountDeleted();
        yield return new AccountNotice.DeletionRequested(Instant, Instant, Zone, "https://example.test/me") { RequestedByPlatform = true };
        yield return new AccountNotice.DeletionRequested(Instant, Instant, Zone, "https://example.test/me") { RequestedByPlatform = true, CanCancel = false };
        yield return new AccountNotice.RecoveryReceived();
        yield return new AccountNotice.RecoveryApproved("https://example.test/login");
        yield return new AccountNotice.RecoveryRejected();
        yield return new AccountNotice.RecoveryRequested(Instant, Zone);
        yield return new AccountNotice.OrganizationApproved("Delta");
        yield return new AccountNotice.OrganizationRejected("Delta");
        yield return new AccountNotice.PublicPageModerated("Delta", true);
    }
    internal static WhatsAppTemplateRenderer Renderer()
    {
        var catalog = new JsonReferenceDataCatalog();
        var formatter = new DisplayFormatter(catalog, catalog, catalog, catalog, catalog, new LibPhoneNumberDisplayFormatter(),
            new TimeZoneService(TimeProvider.System), TimeProvider.System);
        var options = Options.Create(new WhatsAppOptions { PhoneNumberId = "template-number" });
        return new WhatsAppTemplateRenderer(new PlatformNumber(), new WhatsAppTemplateCatalog(options), formatter, new Origin());
    }
    private sealed class Origin : IPublicOrigin { public Uri? Value => new("https://example.test/"); }
    /// <summary>El número de Plataforma conectado con el que salen las plantillas.</summary>
    private sealed class PlatformNumber : ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations.IWhatsAppChannelSettings
    {
        public bool IsEnabled => true;
        public string AppName => "Plataforma";
        public string? PhoneNumberId => "template-number";
        public string? ContactPhone => null;
        public IReadOnlyList<string> Countries => ["AR"];
        public int DailyAuthCodeLimit => 100;
        public ArquitecturaBaseMultitenant.Domain.Results.Result ValidateNewDestination(ArquitecturaBaseMultitenant.Domain.Authentication.LoginCodeDestination destination) =>
            ArquitecturaBaseMultitenant.Domain.Results.Result.Success();
    }
}
