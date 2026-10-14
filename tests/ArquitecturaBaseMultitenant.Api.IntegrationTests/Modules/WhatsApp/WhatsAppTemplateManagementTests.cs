using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B6 de la E11, plantillas de la organización: van a Meta en la cuenta de cada número con su nombre de Meta, la cultura
/// como la pide Meta y los ejemplos; una Marketing lleva «No recibir más». El estado y la categoría siguen a los webhooks.
/// Otra organización no la ve; borrarla la quita de Meta; sin número, Meta no puede recibirla.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppTemplateManagementTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_template_goes_to_Meta_follows_its_webhooks_and_is_deleted_there_too()
    {
        var templates = new FakeTemplates();
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), templates: templates);
        using var owner = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);

        using (var withoutNumber = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/templates", Template("Sin número"), Ct))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, withoutNumber.StatusCode);
            Assert.Equal(WhatsAppErrors.NumberMetaRejectedCode, (await WhatsAppAttentionJourney.JsonAsync(withoutNumber, Ct)).GetProperty("code").GetString());
        }
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct))
            Assert.Equal(HttpStatusCode.Created, connected.StatusCode);
        var wabaId = "waba-" + phoneNumberId;

        using var created = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/templates", Template("Promo de Otoño"), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var template = await WhatsAppAttentionJourney.JsonAsync(created, Ct);
        var id = template.GetProperty("id").GetGuid();
        Assert.Equal("Promo de Otoño", template.GetProperty("name").GetString());
        Assert.Equal("Pending", template.GetProperty("status").GetString());
        Assert.Equal([("es-AR", "Marketing")], Languages(template));
        Assert.Equal([("nombre", "Ana"), ("oferta", "20%")], template.GetProperty("variables").EnumerateArray()
            .Select(value => (value.GetProperty("label").GetString(), value.GetProperty("example").GetString())));
        var (account, submission) = Assert.Single(templates.Created);
        Assert.Equal(wabaId, account);
        Assert.Equal(("promo_de_otono", "es_AR", WhatsAppTemplateCategory.Marketing), (submission.MetaName, submission.MetaLanguage, submission.Category));
        Assert.Equal(["Ana", "20%"], submission.Examples);
        Assert.Equal("No recibir más", submission.UnsubscribeButton);

        using (var taken = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/templates", Template("promo de otoño"), Ct))
        {
            Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
            Assert.Equal(WhatsAppErrors.TemplateNameTakenCode, (await WhatsAppAttentionJourney.JsonAsync(taken, Ct)).GetProperty("code").GetString());
        }
        using (var gaps = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/templates",
            new { name = "Con saltos", category = "Utility", language = "es-AR", body = "Hola {{2}}", variables = new[] { new { label = "dato", example = "x" } } }, Ct))
            Assert.Equal(HttpStatusCode.BadRequest, gaps.StatusCode);

        // Meta la aprueba y después la pasa a Utility: la plantilla sigue a sus webhooks.
        await TemplateWebhookAsync(owner, wabaId, "message_template_status_update", new { @event = "APPROVED", message_template_id = 7_000_001L,
            message_template_name = "promo_de_otono", message_template_language = "es_AR", reason = "NONE", message_template_category = "MARKETING" });
        await TemplateWebhookAsync(owner, wabaId, "template_category_update", new { message_template_id = 7_000_001L,
            message_template_name = "promo_de_otono", message_template_language = "es-AR", previous_category = "MARKETING", new_category = "UTILITY" });
        var approved = await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/templates/{id}", Ct));
        Assert.Equal(("Approved", "Utility"), (approved.GetProperty("status").GetString(), approved.GetProperty("category").GetString()));
        Assert.Equal([("es-AR", "Utility")], Languages(approved));
        var list = await JsonAsync(await owner.GetAsync("/api/organization/whatsapp/templates?status=Approved", Ct));
        Assert.Equal(1, list.GetProperty("approved").GetInt32());
        Assert.Equal(id, Assert.Single(list.GetProperty("templates").GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());

        await TemplateWebhookAsync(owner, wabaId, "message_template_status_update", new { @event = "REJECTED", message_template_id = 7_000_001L,
            message_template_name = "promo_de_otono", message_template_language = "es_AR", reason = "INVALID_FORMAT" });
        var rejected = await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/templates/{id}", Ct));
        Assert.Equal("Rejected", rejected.GetProperty("status").GetString());
        Assert.Contains("formato", rejected.GetProperty("rejectionReason").GetString(), StringComparison.Ordinal);

        // Otra organización no la ve.
        using var other = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, other, Ct);
        using (var foreign = await other.GetAsync($"/api/organization/whatsapp/templates/{id}", Ct)) Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(0, (await JsonAsync(await other.GetAsync("/api/organization/whatsapp/templates", Ct))).GetProperty("templates").GetProperty("totalCount").GetInt32());
        using (var foreignDelete = await other.DeleteAsync($"/api/organization/whatsapp/templates/{id}?version={rejected.GetProperty("version").GetUInt32()}", Ct))
            Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);

        using (var deleted = await owner.DeleteAsync($"/api/organization/whatsapp/templates/{id}?version={rejected.GetProperty("version").GetUInt32()}", Ct))
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal((wabaId, "promo_de_otono"), Assert.Single(templates.Deleted));
        using (var gone = await owner.GetAsync($"/api/organization/whatsapp/templates/{id}", Ct)) Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task The_rate_table_has_the_official_rates_in_force_since_october_2026()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var rates = scope.ServiceProvider.GetRequiredService<IWhatsAppRateReader>();
        var marketing = await rates.FindAsync("AR", WhatsAppPricingCategory.Marketing, new DateOnly(2026, 10, 14), Ct);
        Assert.Equal((0.0618m, "USD"), (marketing!.Amount, marketing.Currency.Value));
        Assert.Equal(0.026m, (await rates.FindAsync("AR", WhatsAppPricingCategory.Service, new DateOnly(2026, 10, 14), Ct))!.Amount);
        Assert.Null(await rates.FindAsync("AR", WhatsAppPricingCategory.Marketing, new DateOnly(2026, 9, 30), Ct));
    }

    private static object Template(string name) => new
    {
        name, category = "Marketing", language = "es-AR", body = "Hola {{1}}, esta semana tenés {{2}} de descuento.",
        variables = new[] { new { label = "nombre", example = "Ana" }, new { label = "oferta", example = "20%" } },
    };

    internal static IEnumerable<(string?, string?)> Languages(JsonElement template) => template.GetProperty("languages").EnumerateArray()
        .Select(value => (value.GetProperty("culture").GetString(), value.GetProperty("category").GetString()));

    internal static async Task TemplateWebhookAsync(HttpClient client, string wabaId, string field, object value)
    {
        var body = JsonSerializer.Serialize(new { @object = "whatsapp_business_account", entry = new[] { new { id = wabaId, time = 1_760_000_000L,
            changes = new[] { new { field, value } } } } });
        using var received = await WhatsAppAttentionJourney.WebhookAsync(client, body, Ct);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using (response) return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }
}

/// <summary>
/// B6 de la E11, plantillas de Plataforma: «Para consultas» son solo Utility; las 16 del sistema se leen con sus dos idiomas
/// (la invitación es Marketing en español y Utility en inglés) y con el estado que trae la sincronización con Meta. Base
/// propia: el número de Plataforma es global.
/// </summary>
public sealed class PlatformWhatsAppTemplateManagementTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Consultation_templates_are_utility_and_system_templates_show_both_languages_and_their_state_in_Meta()
    {
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        var wabaId = "waba-platform-" + phoneNumberId;
        var templates = new FakeTemplates();
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), settings: new Dictionary<string, string>
        {
            ["WhatsApp:PhoneNumberId"] = phoneNumberId, ["WhatsApp:AccessToken"] = "configured-token", ["WhatsApp:BusinessAccountId"] = wabaId,
        }, templates: templates);
        using var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.WhatsApp.Manage], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using (var number = await client.GetAsync("/api/platform/whatsapp/number", Ct)) Assert.Equal(HttpStatusCode.OK, number.StatusCode);

        using (var marketing = await WhatsAppAttentionJourney.PostAsync(client, "/api/platform/whatsapp/templates",
            new { name = "Promo", category = "Marketing", language = "es-AR", body = "Hola", variables = Array.Empty<object>() }, Ct))
            Assert.Equal(HttpStatusCode.BadRequest, marketing.StatusCode);
        using var created = await WhatsAppAttentionJourney.PostAsync(client, "/api/platform/whatsapp/templates",
            new { name = "Seguimiento", category = "Utility", language = "es-AR", body = "Hola {{1}}, seguimos tu consulta.",
                variables = new[] { new { label = "nombre", example = "Ana" } } }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal((wabaId, "seguimiento"), Assert.Single(templates.Created) is var (waba, submission) ? (waba, submission.MetaName) : default);
        Assert.Null(templates.Created.Single().Submission.UnsubscribeButton);

        var consultations = await JsonAsync(await client.GetAsync("/api/platform/whatsapp/templates", Ct));
        Assert.Equal((1, 16), (consultations.GetProperty("consultations").GetInt32(), consultations.GetProperty("system").GetInt32()));
        Assert.Equal("Seguimiento", Assert.Single(consultations.GetProperty("templates").GetProperty("items").EnumerateArray()).GetProperty("name").GetString());

        // Meta tiene el código aprobado en sus dos idiomas y la invitación en español: la sincronización lo trae.
        templates.Accounts[wabaId] =
        [
            new("1", "codigo_ingreso", "es", WhatsAppTemplateStatus.Approved, WhatsAppTemplateCategory.Authentication, null),
            new("2", "codigo_ingreso", "en", WhatsAppTemplateStatus.Approved, WhatsAppTemplateCategory.Authentication, null),
            new("3", "invitacion_organizacion", "es", WhatsAppTemplateStatus.Approved, WhatsAppTemplateCategory.Marketing, null),
            new("4", "seguimiento", "es_AR", WhatsAppTemplateStatus.Approved, WhatsAppTemplateCategory.Utility, null),
        ];
        await host.Services.GetServices<IHostedService>().OfType<WhatsAppTemplateSyncWorker>().Single().RunOnceAsync(Ct);

        var system = await JsonAsync(await client.GetAsync("/api/platform/whatsapp/templates?group=System&pageSize=50", Ct));
        var items = system.GetProperty("templates").GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(16, items.Length);
        Assert.All(items, item => Assert.True(item.GetProperty("isSystem").GetBoolean()));
        var code = items.Single(item => item.GetProperty("name").GetString() == "codigo_ingreso");
        Assert.Equal("Approved", code.GetProperty("status").GetString());
        Assert.Equal("Código de ingreso", code.GetProperty("title").GetString());
        var invitation = items.Single(item => item.GetProperty("name").GetString() == "invitacion_organizacion");
        Assert.Equal([("es", "Marketing"), ("en", "Utility")], WhatsAppTemplateManagementTests.Languages(invitation));
        Assert.Equal(("Pending", "Marketing"), (invitation.GetProperty("status").GetString(), invitation.GetProperty("category").GetString()));
        Assert.Equal(3, invitation.GetProperty("variables").GetArrayLength());
        Assert.Equal(1, system.GetProperty("approved").GetInt32());
        using (var systemDetail = await client.GetAsync($"/api/platform/whatsapp/templates/{code.GetProperty("id").GetGuid()}", Ct))
            Assert.Equal(HttpStatusCode.OK, systemDetail.StatusCode);
        using (var systemDelete = await client.DeleteAsync($"/api/platform/whatsapp/templates/{code.GetProperty("id").GetGuid()}?version=0", Ct))
            Assert.Equal(HttpStatusCode.NotFound, systemDelete.StatusCode);
        var followUp = Assert.Single((await JsonAsync(await client.GetAsync("/api/platform/whatsapp/templates?group=Consultations", Ct)))
            .GetProperty("templates").GetProperty("items").EnumerateArray());
        Assert.Equal("Approved", followUp.GetProperty("status").GetString());
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using (response) return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }
}
