using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

        // Una difusión es un aviso: sale después de lo que contesta. Si la cola no sabe si salió, el destinatario dice
        // «Sin confirmar» y no cuenta como enviado ni como «No llegó» (decisión 255).
        var queued = Assert.Single(await QueuedAsync(host), value => value.Message.TemplateName == "promo_de_la_semana");
        Assert.Equal(OutboxPriority.Notice, queued.Row.Priority);
        await OutboxRows.SetStatusAsync(host.Services, queued.Row.Id, OutboxStatus.Uncertain, Ct);
        var unconfirmed = await DetailAsync(owner, nowId);
        Assert.Contains((anaId, "Uncertain"), Recipients(unconfirmed));
        Assert.Equal((0, 0, 1), (unconfirmed.GetProperty("broadcast").GetProperty("delivered").GetInt32(),
            unconfirmed.GetProperty("broadcast").GetProperty("failed").GetInt32(), unconfirmed.GetProperty("broadcast").GetProperty("uncertain").GetInt32()));
        var listed = Assert.Single((await JsonAsync(owner.GetAsync("/api/organization/whatsapp/broadcasts?status=Sent", Ct))).GetProperty("broadcasts")
            .GetProperty("items").EnumerateArray());
        Assert.Equal((nowId, 0, 0, 1), (listed.GetProperty("id").GetGuid(), listed.GetProperty("delivered").GetInt32(),
            listed.GetProperty("failed").GetInt32(), listed.GetProperty("uncertain").GetInt32()));
        Assert.Equal(anaId, Assert.Single((await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/broadcasts/{nowId}?status=Uncertain", Ct)))
            .GetProperty("recipients").GetProperty("items").EnumerateArray()).GetProperty("contactId").GetGuid());
        await OutboxRows.SetStatusAsync(host.Services, queued.Row.Id, OutboxStatus.Pending, Ct);

        // Ana contesta: respondió. Después toca «No recibir más»: se da de baja y el bot lo confirma.
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, ana, "¿Hasta cuándo?", Ct);
        Assert.Contains((anaId, "Replied"), Recipients(await DetailAsync(owner, nowId)));
        Assert.Equal(1, (await DetailAsync(owner, nowId)).GetProperty("broadcast").GetProperty("replied").GetInt32());
        await ButtonAsync(host, anonymous, phoneNumberId, ana, BotButtons.Unsubscribe);
        var afterUnsubscribe = await DetailAsync(owner, nowId);
        Assert.Contains((anaId, "Unsubscribed"), Recipients(afterUnsubscribe));
        Assert.Equal(2, afterUnsubscribe.GetProperty("broadcast").GetProperty("unsubscribed").GetInt32());
        Assert.Equal("Unsubscribed", (await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/contacts/{anaId}", Ct))).GetProperty("consent").GetString());
        var confirmation = Assert.Single(await QueuedAsync(host), value => value.Message.To == ana
            && value.Message.Body?.Contains("promociones", StringComparison.Ordinal) == true);
        // La confirmación del bot contesta: va con prioridad y en el mismo chat que la difusión a Ana.
        Assert.Equal((OutboxPriority.Reply, queued.Row.OrderingKey), (confirmation.Row.Priority, confirmation.Row.OrderingKey));
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

    // Decisión 264: las tablas de WhatsApp ordenan por cada columna con un dato, en los dos sentidos, con lo vacío al final y
    // un estado por su circuito; una columna que no está en la lista es un 400.
    [Fact]
    public async Task Every_whatsapp_table_sorts_by_its_columns_with_empty_values_last()
    {
        var templates = new FakeTemplates();
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), templates: templates);
        using var owner = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct)) Assert.Equal(HttpStatusCode.Created, connected.StatusCode);
        var promo = await TemplateAsync(owner, "Promo ordenada", "Marketing", "Hola, esta semana tenés {{1}} de descuento.");
        await TemplateAsync(owner, "Aviso ordenado", "Utility", "Tu turno es el {{1}}.");
        await WhatsAppTemplateManagementTests.TemplateWebhookAsync(owner, "waba-" + phoneNumberId, "message_template_status_update",
            new { @event = "APPROVED", message_template_id = 1L, message_template_name = "promo_ordenada", message_template_language = "es_AR", reason = "NONE" });
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var (ana, beto, caro) = (WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone());
        foreach (var phone in new[] { ana, beto, caro }) await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Hola", Ct);
        var anaId = await ConsentAsync(owner, ana, "Accepted", []);
        var betoId = await ConsentAsync(owner, beto, "Declined", []);
        var caroId = Assert.Single(await IdsAsync(owner, $"contacts?search={Uri.EscapeDataString(caro)}", "contacts"));
        var scheduled = (await CreateAsync(owner, promo, host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(1)))
            .GetProperty("id").GetGuid();
        var sent = (await CreateAsync(owner, promo, null)).GetProperty("id").GetGuid();
        await DispatchAsync(host);

        // El consentimiento, por su circuito (acepta, no acepta, sin respuesta) y no por su nombre.
        Assert.Equal([anaId, betoId, caroId], await IdsAsync(owner, "contacts?sort=consent", "contacts"));
        Assert.Equal([caroId, betoId, anaId], await IdsAsync(owner, "contacts?sort=-consent", "contacts"));
        // Entregados: la programada todavía no salió (la raya) y va al final en los dos sentidos.
        Assert.Equal([sent, scheduled], await IdsAsync(owner, "broadcasts?sort=delivered", "broadcasts"));
        Assert.Equal([sent, scheduled], await IdsAsync(owner, "broadcasts?sort=-delivered", "broadcasts"));
        // El estado de una difusión, por su circuito: programada antes que enviada.
        Assert.Equal([scheduled, sent], await IdsAsync(owner, "broadcasts?sort=status", "broadcasts"));

        foreach (var (path, fields) in new[]
        {
            ("contacts", WhatsAppContactListRequest.SortableFields),
            ("broadcasts", WhatsAppBroadcastListRequest.SortableFields),
            ($"broadcasts/{sent}", WhatsAppBroadcastDetailRequest.SortableFields),
            ("templates", WhatsAppTemplateListRequest.SortableFields),
            ("teams", WhatsAppTeamListRequest.SortableFields),
        })
        {
            foreach (var sort in fields.SelectMany(field => new[] { field, "-" + field }))
            {
                using var response = await owner.GetAsync($"/api/organization/whatsapp/{path}?sort={sort}", Ct);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}?sort={sort}: {(int)response.StatusCode}");
            }
        }

        await ProblemAsync(owner.GetAsync("/api/organization/whatsapp/contacts?sort=tags", Ct), HttpStatusCode.BadRequest, "Validation.Failed");
    }

    [Fact]
    public async Task A_broadcast_waits_while_the_whatsapp_module_is_disabled_and_another_organization_keeps_sending()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), templates: new FakeTemplates());
        using var ownerA = WhatsAppAttentionJourney.Client(host);
        var (_, tenantA) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, ownerA, Ct);
        using var ownerB = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, ownerB, Ct);
        using var anonymous = WhatsAppAttentionJourney.Client(host);

        var (ana, beto) = (WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone());
        var broadcastA = await StageDueBroadcastAsync(host, ownerA, anonymous, "Promo pausa", "promo_pausa", ana);
        var broadcastB = await StageDueBroadcastAsync(host, ownerB, anonymous, "Promo activa", "promo_activa", beto);

        // Se apaga WhatsApp solo en la organización A: su difusión espera sin cancelarse y B sigue enviando.
        await SetModuleEnabledAsync(host, tenantA, false);
        await DispatchAsync(host);

        Assert.Equal("Sent", (await DetailAsync(ownerB, broadcastB)).GetProperty("broadcast").GetProperty("status").GetString());
        Assert.Contains(await OutboxAsync(host), message => message.To == beto && message.TemplateName == "promo_activa");
        Assert.DoesNotContain(await OutboxAsync(host), message => message.To == ana && message.TemplateName == "promo_pausa");

        // Al volver a prender el módulo, la difusión de A seguía programada y sale en la vuelta siguiente.
        await SetModuleEnabledAsync(host, tenantA, true);
        Assert.Equal("Scheduled", (await DetailAsync(ownerA, broadcastA)).GetProperty("broadcast").GetProperty("status").GetString());
        await DispatchAsync(host);
        Assert.Equal("Sent", (await DetailAsync(ownerA, broadcastA)).GetProperty("broadcast").GetProperty("status").GetString());
        Assert.Contains(await OutboxAsync(host), message => message.To == ana && message.TemplateName == "promo_pausa");
    }

    [Fact]
    public async Task Queued_broadcast_messages_never_reach_meta_while_the_module_is_off_and_each_recipient_gets_one_when_it_is_back()
    {
        var meta = new FakeMeta();
        await using var host = MetaHost(meta);
        using var ownerA = WhatsAppAttentionJourney.Client(host);
        var (_, tenantA) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, ownerA, Ct);
        using var ownerB = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, ownerB, Ct);
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var (ana, beto, caro) = (WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone());
        var broadcastA = await StageDueBroadcastAsync(host, ownerA, anonymous, "Promo en cola", "promo_en_cola", ana, beto);
        await StageDueBroadcastAsync(host, ownerB, anonymous, "Promo vecina", "promo_vecina", caro);

        // Las dos difusiones quedan enteras en la cola, y Plataforma apaga WhatsApp de A antes de que salgan.
        await DispatchAsync(host);
        Assert.Equal("Sent", Status(await DetailAsync(ownerA, broadcastA)));
        await SetModuleEnabledAsync(host, tenantA, false);

        // Corre la cola: lo de A no llega a Meta y lo de B sale.
        await DeliverAsync(host);
        Assert.Equal((0, 0, 1), (meta.Count(ana, "promo_en_cola"), meta.Count(beto, "promo_en_cola"), meta.Count(caro, "promo_vecina")));

        // La vuelta de las difusiones saca de la cola lo de A, y no sale nada.
        await DispatchAsync(host);
        await DeliverAsync(host);
        Assert.Equal((0, 0), (meta.Count(ana, "promo_en_cola"), meta.Count(beto, "promo_en_cola")));

        // Apagado, sus rutas dan 404; al prenderlo, la difusión estaba en pausa, con sus destinatarios pendientes.
        await SetModuleEnabledAsync(host, tenantA, true);
        var paused = await DetailAsync(ownerA, broadcastA);
        Assert.Equal("Sending", Status(paused));
        Assert.All(Recipients(paused), recipient => Assert.Equal("Pending", recipient.Item2));

        // Sigue sola: cada destinatario recibe uno, una sola vez.
        await DispatchAsync(host);
        await DeliverAsync(host);
        await DispatchAsync(host);
        await DeliverAsync(host);
        Assert.Equal((1, 1, 1), (meta.Count(ana, "promo_en_cola"), meta.Count(beto, "promo_en_cola"), meta.Count(caro, "promo_vecina")));
        var resumed = await DetailAsync(ownerA, broadcastA);
        Assert.Equal("Sent", Status(resumed));
        Assert.All(Recipients(resumed), recipient => Assert.Equal("Sent", recipient.Item2));
    }

    [Fact]
    public async Task A_broadcast_message_held_by_the_queue_goes_out_once_without_spending_attempts_when_the_module_is_back()
    {
        var meta = new FakeMeta();
        await using var host = MetaHost(meta);
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var ana = WhatsAppAttentionJourney.NewPhone();
        await StageDueBroadcastAsync(host, owner, anonymous, "Promo retenida", "promo_retenida", ana);
        await DispatchAsync(host);

        // Con el módulo apagado, la cola no lo manda ni le gasta un intento: espera.
        await SetModuleEnabledAsync(host, tenant, false);
        await DeliverAsync(host);
        Assert.Equal(0, meta.Count(ana, "promo_retenida"));
        var held = Assert.Single(await QueuedAsync(host), value => value.Message.To == ana && value.Message.TemplateName == "promo_retenida").Row;
        Assert.Equal((OutboxStatus.Pending, 0, (Guid?)null), (held.Status, held.Attempts, held.LeaseId));

        // Se prende antes de que la vuelta de las difusiones lo saque: cuando le toca otra vez, sale una sola vez.
        await SetModuleEnabledAsync(host, tenant, true);
        await OutboxRows.MakeDueAsync(host.Services, held.Id, Ct);
        await DeliverAsync(host);
        await OutboxRows.MakeDueAsync(host.Services, held.Id, Ct);
        await DeliverAsync(host);
        Assert.Equal(1, meta.Count(ana, "promo_retenida"));
        await using var read = host.Services.CreateAsyncScope();
        var sent = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == held.Id, Ct);
        Assert.Equal((OutboxStatus.Sent, 1), (sent.Status, sent.Attempts));
    }

    [Fact]
    public async Task A_failed_status_webhook_records_its_reason_on_the_broadcast_recipient()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), templates: new FakeTemplates());
        using var owner = WhatsAppAttentionJourney.Client(host);
        owner.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-AR");
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct))
            Assert.Equal(HttpStatusCode.Created, connected.StatusCode);
        var templateId = await TemplateAsync(owner, "Promo fallida", "Marketing", "Hola, tenés {{1}} de descuento.");
        await WhatsAppTemplateManagementTests.TemplateWebhookAsync(owner, "waba-" + phoneNumberId, "message_template_status_update",
            new { @event = "APPROVED", message_template_id = 1L, message_template_name = "promo_fallida", message_template_language = "es_AR", reason = "NONE" });

        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var (ana, beto) = (WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone());
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, ana, "Hola", Ct);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, beto, "Hola", Ct);
        var anaId = await ConsentAsync(owner, ana, "Accepted", []);
        var betoId = await ConsentAsync(owner, beto, "Accepted", []);

        var broadcastId = (await CreateAsync(owner, templateId, null)).GetProperty("id").GetGuid();
        await DispatchAsync(host);

        var queued = await QueuedAsync(host);
        var anaOutbox = Assert.Single(queued, item => item.Message.To == ana && item.Message.TemplateName == "promo_fallida").Row.Id;
        var betoOutbox = Assert.Single(queued, item => item.Message.To == beto && item.Message.TemplateName == "promo_fallida").Row.Id;
        var wamidAna = "wamid.bc.131049." + Guid.NewGuid().ToString("N");
        var wamidBeto = "wamid.bc.131026." + Guid.NewGuid().ToString("N");
        var nowUtc = host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var observer = scope.ServiceProvider.GetServices<IOutboxDeliveryObserver>().Single(value => value.Key == "whatsapp");
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await observer.RecordAcceptedAsync(anaOutbox, OutboxDeliveryReceipt.Accepted(wamidAna, phoneNumberId, "Template"), nowUtc, ct);
                await observer.RecordAcceptedAsync(betoOutbox, OutboxDeliveryReceipt.Accepted(wamidBeto, phoneNumberId, "Template"), nowUtc, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        using (var failed = await WhatsAppAttentionJourney.WebhookAsync(anonymous, WhatsAppAttentionJourney.Statuses(phoneNumberId,
            WhatsAppAttentionJourney.Status(wamidAna, "failed", error: 131049),
            WhatsAppAttentionJourney.Status(wamidBeto, "failed", error: 131026)), Ct))
            Assert.Equal(HttpStatusCode.OK, failed.StatusCode);

        // El webhook deja cada motivo en la cola durable, en su misma transacción; lo anota el procesador dentro del alcance
        // de la organización, así una caída después del commit no lo pierde.
        Assert.Equal(new Dictionary<string, string?> { [wamidAna] = WhatsAppSendFailures.RateLimited, [wamidBeto] = WhatsAppSendFailures.Undeliverable },
            await WhatsAppAttentionJourney.PendingDeliveryFailuresAsync(host, [wamidAna, wamidBeto], Ct));
        await WhatsAppAttentionJourney.ProcessInboundAsync(host, Ct);
        Assert.Empty(await WhatsAppAttentionJourney.PendingDeliveryFailuresAsync(host, [wamidAna, wamidBeto], Ct));

        await using (var verify = host.Services.CreateAsyncScope())
        {
            verify.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
            var rows = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppBroadcastRecipient>()
                .AsNoTracking().Where(value => value.BroadcastId == broadcastId).ToArrayAsync(Ct);
            var anaRow = Assert.Single(rows, value => value.CustomerId == anaId);
            var betoRow = Assert.Single(rows, value => value.CustomerId == betoId);
            Assert.Equal((WhatsAppRecipientStatus.Failed, WhatsAppSendFailures.RateLimited), (anaRow.Status, anaRow.FailureReason));
            Assert.Equal((WhatsAppRecipientStatus.Failed, WhatsAppSendFailures.Undeliverable), (betoRow.Status, betoRow.FailureReason));
        }

        var detail = await DetailAsync(owner, broadcastId);
        Assert.Equal(2, detail.GetProperty("broadcast").GetProperty("failed").GetInt32());
        var items = detail.GetProperty("recipients").GetProperty("items").EnumerateArray().ToArray();
        var anaItem = Assert.Single(items, value => value.GetProperty("contactId").GetGuid() == anaId);
        var betoItem = Assert.Single(items, value => value.GetProperty("contactId").GetGuid() == betoId);
        Assert.Equal(("Failed", "WhatsApp frenó el envío por su límite de mensajes. Probá de nuevo en un rato."),
            (anaItem.GetProperty("status").GetString(), anaItem.GetProperty("failureReason").GetString()));
        Assert.Equal(("Failed", "WhatsApp no pudo entregarlo a ese número."),
            (betoItem.GetProperty("status").GetString(), betoItem.GetProperty("failureReason").GetString()));
    }

    private static async Task<Guid> StageDueBroadcastAsync(WebApplicationFactory<Program> host, HttpClient owner, HttpClient anonymous,
        string templateName, string metaName, params string[] phones)
    {
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct))
            Assert.Equal(HttpStatusCode.Created, connected.StatusCode);
        var templateId = await TemplateAsync(owner, templateName, "Marketing", "Hola, tenés {{1}} de descuento.");
        await WhatsAppTemplateManagementTests.TemplateWebhookAsync(owner, "waba-" + phoneNumberId, "message_template_status_update",
            new { @event = "APPROVED", message_template_id = 1L, message_template_name = metaName, message_template_language = "es_AR", reason = "NONE" });
        foreach (var phone in phones)
        {
            await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Hola", Ct);
            await ConsentAsync(owner, phone, "Accepted", []);
        }
        return (await CreateAsync(owner, templateId, null)).GetProperty("id").GetGuid();
    }

    /// <summary>Un host cuya Cloud API es <paramref name="meta"/>: la cola manda de verdad, sin salir de la prueba.</summary>
    private WebApplicationFactory<Program> MetaHost(FakeMeta meta) =>
        WhatsAppAttentionJourney.Host(factory, new FakeGraph(), templates: new FakeTemplates()).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddHttpClient<WhatsAppCloudClient>().ConfigurePrimaryHttpMessageHandler(() => meta)));

    /// <summary>Corre la cola de WhatsApp como el despacho, que en las pruebas no corre solo, hasta que no queda nada que le toque.</summary>
    private static async Task DeliverAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var senders = scope.ServiceProvider.GetServices<IChannelSender>().Where(sender => sender.Key == "whatsapp").ToArray();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxDeliveryProcessor>();
        for (var processed = 0; processed < 500 && await processor.ProcessOnceAsync(senders, Ct) > 0; processed++) { }
    }

    private static string? Status(JsonElement detail) => detail.GetProperty("broadcast").GetProperty("status").GetString();

    private static async Task SetModuleEnabledAsync(WebApplicationFactory<Program> host, Guid tenantId, bool enabled)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var feature = await services.GetRequiredService<ApplicationDbContext>().TenantFeatures
                .SingleAsync(value => value.TenantId == tenantId && value.FeatureKey == WhatsAppFeature.Key, ct);
            Assert.True(feature.Update(enabled, feature.Source, null).IsSuccess);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(
                AuditActorKind.System, Guid.Empty, tenantId, "WhatsApp broadcast module fixture",
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<Guid[]> IdsAsync(HttpClient owner, string path, string list) =>
        [.. (await JsonAsync(owner.GetAsync("/api/organization/whatsapp/" + path, Ct))).GetProperty(list).GetProperty("items").EnumerateArray()
            .Select(value => value.GetProperty("id").GetGuid())];

    private static async Task<Guid> TemplateAsync(HttpClient owner, string name, string category, string body)
    {
        var count = WhatsAppTemplateBase.Placeholders(body);
        using var created = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/templates", new
        {
            name,
            category,
            language = "es-AR",
            body,
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
            name = "Promo " + Guid.NewGuid().ToString("N")[..6],
            templateId,
            variables = new[] { "20%" },
            audience = new { kind = "AllAccepting" },
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

    private static async Task<IReadOnlyList<(OutboxMessage Row, WhatsAppOutboundMessage Message)>> QueuedAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var protector = scope.ServiceProvider.GetRequiredService<IPayloadProtector>();
        var rows = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(value => value.Channel == "whatsapp" && value.EncryptedPayload != string.Empty).ToArrayAsync(Ct);
        var queued = new List<(OutboxMessage, WhatsAppOutboundMessage)>();
        foreach (var row in rows)
        {
            try { queued.Add((row, JsonSerializer.Deserialize<WhatsAppOutboundMessage>(protector.Unprotect(row.EncryptedPayload))!)); }
            catch (System.Security.Cryptography.CryptographicException) { }
        }
        return queued;
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

    /// <summary>La Cloud API de Meta: anota a quién le llegó cada plantilla y la acepta.</summary>
    private sealed class FakeMeta : HttpMessageHandler
    {
        private readonly ConcurrentQueue<(string? To, string? Template)> _received = new();

        /// <summary>Cuántas veces le llegó esa plantilla a ese teléfono (Meta lo recibe sin el «+» y, en la Argentina, sin el 9).</summary>
        internal int Count(string phone, string template) =>
            _received.Count(value => value.Template == template && value.To is { } to && to.EndsWith(phone[^8..], StringComparison.Ordinal));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var root = body.RootElement;
            _received.Enqueue((root.TryGetProperty("to", out var to) ? to.GetString() : null,
                root.TryGetProperty("template", out var template) && template.TryGetProperty("name", out var name) ? name.GetString() : null));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"messages\":[{\"id\":\"wamid.fake." + Guid.NewGuid().ToString("N") + "\"}]}"),
            };
        }
    }
}
