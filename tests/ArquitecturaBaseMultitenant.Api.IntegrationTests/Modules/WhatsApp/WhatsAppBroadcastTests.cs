using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B6 de la E11, difusiones: solo con una Marketing aprobada y solo a quienes aceptan promociones, con el costo de la tarifa
/// vigente; respetan el tope de Meta; quien se da de baja entre programar y enviar no recibe; contestar cuenta como respuesta
/// y «No recibir más» da de baja. Una programada se cancela; una enviada, no. En la conversación sale una plantilla aprobada
/// (Marketing solo a quien acepta) y una pendiente no se puede usar.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppBroadcastTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_broadcast_reaches_only_who_accepts_at_send_time_and_follows_replies_and_unsubscribes()
    {
        var templates = new FakeTemplates();
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), templates: templates);
        using var owner = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct)) Assert.Equal(HttpStatusCode.Created, connected.StatusCode);
        var wabaId = "waba-" + phoneNumberId;
        var promo = await TemplateAsync(owner, "Promo de la semana", "Marketing", "Hola, esta semana tenés {{1}} de descuento.");
        var notice = await TemplateAsync(owner, "Aviso de turno", "Utility", "Tu turno es el {{1}}.");
        var pending = await TemplateAsync(owner, "Todavía pendiente", "Utility", "Hola.");
        foreach (var name in new[] { "promo_de_la_semana", "aviso_de_turno" })
            await WhatsAppTemplateManagementTests.TemplateWebhookAsync(owner, wabaId, "message_template_status_update",
                new { @event = "APPROVED", message_template_id = 1L, message_template_name = name, message_template_language = "es_AR", reason = "NONE" });

        // Tres clientes escriben: dos aceptan promociones (uno con la etiqueta vip) y uno no contestó.
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var (ana, beto, caro) = (WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone());
        foreach (var phone in new[] { ana, beto, caro }) await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Hola", Ct);
        var anaId = await ConsentAsync(owner, ana, "Accepted", ["vip"]);
        var betoId = await ConsentAsync(owner, beto, "Accepted", []);

        var estimate = await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/broadcasts/estimate?templateId={promo}&kind=AllAccepting", Ct));
        Assert.Equal((2, "AR"), (estimate.GetProperty("recipients").GetInt32(), estimate.GetProperty("country").GetString()));
        Assert.Equal((0.12m, "USD"), Money(estimate.GetProperty("estimatedCost")));
        Assert.Equal(1, (await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/broadcasts/estimate?templateId={promo}&kind=Tag&tag=vip", Ct)))
            .GetProperty("recipients").GetInt32());
        await ProblemAsync(owner.GetAsync($"/api/organization/whatsapp/broadcasts/estimate?templateId={promo}&kind=Tag&tag=nadie", Ct),
            HttpStatusCode.BadRequest, WhatsAppErrors.BroadcastNoRecipientsCode);
        await ProblemAsync(owner.GetAsync($"/api/organization/whatsapp/broadcasts/estimate?templateId={notice}&kind=AllAccepting", Ct),
            HttpStatusCode.Conflict, WhatsAppErrors.TemplateNotApprovedCode);
        var option = Assert.Single((await JsonAsync(await owner.GetAsync("/api/organization/whatsapp/broadcasts/template-options", Ct)))
            .GetProperty("items").EnumerateArray());
        Assert.Equal((promo, (0.06m, "USD")), (option.GetProperty("templateId").GetGuid(), Money(option.GetProperty("estimatedCost"))));

        // Una programada para mañana se cancela; ya cancelada, no.
        var later = await CreateAsync(owner, promo, host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(1));
        Assert.Equal(("Scheduled", 2), (later.GetProperty("status").GetString(), later.GetProperty("recipients").GetInt32()));
        Assert.Equal(["20%"], later.GetProperty("variables").EnumerateArray().Select(value => value.GetString()));
        var cancelled = await JsonAsync(await WhatsAppAttentionJourney.PostAsync(owner, $"/api/organization/whatsapp/broadcasts/{later.GetProperty("id").GetGuid()}/cancel",
            new { version = later.GetProperty("version").GetUInt32() }, Ct));
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        await ProblemAsync(WhatsAppAttentionJourney.PostAsync(owner, $"/api/organization/whatsapp/broadcasts/{later.GetProperty("id").GetGuid()}/cancel",
            new { version = cancelled.GetProperty("version").GetUInt32() }, Ct), HttpStatusCode.Conflict, WhatsAppErrors.BroadcastNotCancellableCode);

        // Una que sale ya: Beto deja de aceptar antes de que salga; el tope de Meta en 0 la frena hasta que hay lugar.
        var now = await CreateAsync(owner, promo, null);
        var nowId = now.GetProperty("id").GetGuid();
        await ConsentAsync(owner, beto, "Declined", []);
        templates.MessagingLimit = 0;
        await DispatchAsync(host);
        Assert.Equal("Scheduled", (await DetailAsync(owner, nowId)).GetProperty("broadcast").GetProperty("status").GetString());
        templates.MessagingLimit = null;
        await DispatchAsync(host);
        var sent = (await OutboxAsync(host)).Where(message => message.TemplateName == "promo_de_la_semana").ToArray();
        var toAna = Assert.Single(sent);
        Assert.Equal((ana, "es_AR", BotButtons.Unsubscribe), (toAna.To, toAna.Language, toAna.QuickReplyPayload));
        Assert.Equal(["20%"], toAna.BodyParameters);
        var detail = await DetailAsync(owner, nowId);
        Assert.Equal("Sent", detail.GetProperty("broadcast").GetProperty("status").GetString());
        Assert.Equal([(anaId, "Sent"), (betoId, "Unsubscribed")], Recipients(detail).OrderBy(value => value.Item1 == betoId));

        // Ana contesta: respondió. Después toca «No recibir más»: se da de baja y el bot lo confirma.
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, ana, "¿Hasta cuándo?", Ct);
        Assert.Contains((anaId, "Replied"), Recipients(await DetailAsync(owner, nowId)));
        Assert.Equal(1, (await DetailAsync(owner, nowId)).GetProperty("broadcast").GetProperty("replied").GetInt32());
        await ButtonAsync(host, anonymous, phoneNumberId, ana, BotButtons.Unsubscribe);
        var afterUnsubscribe = await DetailAsync(owner, nowId);
        Assert.Contains((anaId, "Unsubscribed"), Recipients(afterUnsubscribe));
        Assert.Equal(2, afterUnsubscribe.GetProperty("broadcast").GetProperty("unsubscribed").GetInt32());
        Assert.Equal("Unsubscribed", (await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/contacts/{anaId}", Ct))).GetProperty("consent").GetString());
        Assert.Contains(await OutboxAsync(host), message => message.To == ana && message.Body?.Contains("promociones", StringComparison.Ordinal) == true);
        var tab = Assert.Single((await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/contacts/{anaId}/broadcasts", Ct))).GetProperty("items").EnumerateArray());
        Assert.Equal((nowId, "Unsubscribed"), (tab.GetProperty("broadcastId").GetGuid(), tab.GetProperty("status").GetString()));
        await ProblemAsync(WhatsAppAttentionJourney.PostAsync(owner, $"/api/organization/whatsapp/broadcasts/{nowId}/cancel",
            new { version = afterUnsubscribe.GetProperty("broadcast").GetProperty("version").GetUInt32() }, Ct), HttpStatusCode.Conflict,
            WhatsAppErrors.BroadcastNotCancellableCode);

        // En la conversación de Caro (no aceptó promociones) solo se ofrece la Utility; sale con sus valores y una pendiente no.
        var caroChat = Assert.Single((await JsonAsync(await owner.GetAsync(
            $"/api/organization/whatsapp/customers/conversations?search={Uri.EscapeDataString(caro)}", Ct))).GetProperty("conversations")
            .GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        var options = (await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/customers/conversations/{caroChat}/template-options", Ct)))
            .GetProperty("items").EnumerateArray().Select(value => value.GetProperty("templateId").GetGuid()).ToArray();
        Assert.Equal([notice], options);
        await ProblemAsync(WhatsAppAttentionJourney.PostAsync(owner, $"/api/organization/whatsapp/customers/conversations/{caroChat}/templates",
            new { templateId = promo, variables = new[] { "20%" } }, Ct), HttpStatusCode.Conflict, WhatsAppErrors.TemplateNotApprovedCode);
        await ProblemAsync(WhatsAppAttentionJourney.PostAsync(owner, $"/api/organization/whatsapp/customers/conversations/{caroChat}/templates",
            new { templateId = pending, variables = Array.Empty<string>() }, Ct), HttpStatusCode.Conflict, WhatsAppErrors.TemplateNotApprovedCode);
        using var withTemplate = await WhatsAppAttentionJourney.PostAsync(owner, $"/api/organization/whatsapp/customers/conversations/{caroChat}/templates",
            new { templateId = notice, variables = new[] { "lunes 20" } }, Ct);
        Assert.Equal(HttpStatusCode.Created, withTemplate.StatusCode);
        var message = await WhatsAppAttentionJourney.JsonAsync(withTemplate, Ct);
        Assert.Equal(("Template", "Tu turno es el lunes 20."), (message.GetProperty("kind").GetString(), message.GetProperty("text").GetString()));
        var toCaro = Assert.Single(await OutboxAsync(host), value => value.To == caro && value.TemplateName == "aviso_de_turno");
        Assert.Equal(["lunes 20"], toCaro.BodyParameters);
        Assert.Null(toCaro.QuickReplyPayload);

        // Otra organización no ve ni cancela una difusión ajena.
        using var other = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, other, Ct);
        using (var foreign = await other.GetAsync($"/api/organization/whatsapp/broadcasts/{nowId}", Ct)) Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(0, (await JsonAsync(other.GetAsync("/api/organization/whatsapp/broadcasts", Ct))).GetProperty("broadcasts").GetProperty("totalCount").GetInt32());
        await ProblemAsync(WhatsAppAttentionJourney.PostAsync(other, $"/api/organization/whatsapp/broadcasts/{later.GetProperty("id").GetGuid()}/cancel",
            new { version = cancelled.GetProperty("version").GetUInt32() }, Ct), HttpStatusCode.NotFound, WhatsAppErrors.BroadcastNotFoundCode);
    }

    private static async Task<Guid> TemplateAsync(HttpClient owner, string name, string category, string body)
    {
        var count = WhatsAppTemplateBase.Placeholders(body);
        using var created = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/templates", new
        {
            name, category, language = "es-AR", body,
            variables = Enumerable.Range(1, count).Select(index => new { label = "dato " + index, example = "ejemplo" }).ToArray(),
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await WhatsAppAttentionJourney.JsonAsync(created, Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> ConsentAsync(HttpClient owner, string phone, string consent, string[] tags)
    {
        var contact = Assert.Single((await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/contacts?search={Uri.EscapeDataString(phone)}", Ct)))
            .GetProperty("contacts").GetProperty("items").EnumerateArray());
        var id = contact.GetProperty("id").GetGuid();
        using var updated = await owner.PutAsJsonAsync($"/api/organization/whatsapp/contacts/{id}",
            new { name = "Cliente", tags, consent, version = contact.GetProperty("version").GetUInt32() }, Ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        return id;
    }

    private static async Task<JsonElement> CreateAsync(HttpClient owner, Guid templateId, DateTime? scheduledAtUtc)
    {
        using var created = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/broadcasts", new
        {
            name = "Promo " + Guid.NewGuid().ToString("N")[..6], templateId, variables = new[] { "20%" }, audience = new { kind = "AllAccepting" },
            scheduledAtUtc,
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return await WhatsAppAttentionJourney.JsonAsync(created, Ct);
    }

    private static Task<JsonElement> DetailAsync(HttpClient owner, Guid id) => JsonAsync(owner.GetAsync($"/api/organization/whatsapp/broadcasts/{id}", Ct));

    private static IEnumerable<(Guid, string?)> Recipients(JsonElement detail) => detail.GetProperty("recipients").GetProperty("items").EnumerateArray()
        .Select(value => (value.GetProperty("contactId").GetGuid(), value.GetProperty("status").GetString()));

    private static (decimal, string?) Money(JsonElement money) => (money.GetProperty("amount").GetDecimal(), money.GetProperty("currency").GetString());

    private static Task DispatchAsync(WebApplicationFactory<Program> host) =>
        host.Services.GetServices<IHostedService>().OfType<WhatsAppBroadcastWorker>().Single().RunOnceAsync(Ct);

    private static async Task ButtonAsync(WebApplicationFactory<Program> host, HttpClient client, string phoneNumberId, string from, string payload)
    {
        var body = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-" + phoneNumberId, changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
                messages = new[] { new
                {
                    from = from.TrimStart('+'), id = "wamid.button." + Guid.NewGuid().ToString("N"), type = "button",
                    button = new { payload, text = "No recibir más" },
                    timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                } },
            } } } } },
        });
        using var received = await WhatsAppAttentionJourney.WebhookAsync(client, body, Ct);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await host.Services.GetServices<IHostedService>().OfType<Infrastructure.Modules.WhatsApp.Inbound.WhatsAppInboundProcessor>().Single()
            .ProcessPendingAsync(Ct);
    }

    private static async Task<IReadOnlyList<WhatsAppOutboundMessage>> OutboxAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var protector = scope.ServiceProvider.GetRequiredService<IPayloadProtector>();
        var rows = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(value => value.Channel == "whatsapp").OrderBy(value => value.CreatedAtUtc).ThenBy(value => value.Id).ToArrayAsync(Ct);
        var messages = new List<WhatsAppOutboundMessage>();
        foreach (var row in rows)
        {
            try { messages.Add(JsonSerializer.Deserialize<WhatsAppOutboundMessage>(protector.Unprotect(row.EncryptedPayload))!); }
            catch (System.Security.Cryptography.CryptographicException) { }
        }
        return messages;
    }

    private static async Task ProblemAsync(Task<HttpResponseMessage> request, HttpStatusCode status, string code)
    {
        using var response = await request;
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await WhatsAppAttentionJourney.JsonAsync(response, Ct)).GetProperty("code").GetString());
    }

    private static async Task<JsonElement> JsonAsync(Task<HttpResponseMessage> request) => await JsonAsync(await request);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using (response) return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }
}
