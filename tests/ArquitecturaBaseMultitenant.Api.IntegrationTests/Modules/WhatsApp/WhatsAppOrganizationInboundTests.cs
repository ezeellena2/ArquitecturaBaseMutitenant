using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B2 de la E11: la entrada de cada número va a su organización (por el identificador de Meta, nunca por el remitente), un
/// miembro activo con ese teléfono va a Internos y uno que dejó de serlo, a Clientes; con el módulo apagado se conserva.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppOrganizationInboundTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_organizations_with_their_own_numbers_never_see_each_other_messages()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var first = WhatsAppAttentionJourney.Client(host);
        using var second = WhatsAppAttentionJourney.Client(host);
        var (_, tenantA) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, first, Ct);
        var (_, tenantB) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, second, Ct);
        var numberA = WhatsAppAttentionJourney.NewNumberId(); var numberB = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(first, numberA, Ct)).Dispose();
        (await WhatsAppAttentionJourney.ConnectAsync(second, numberB, Ct)).Dispose();
        var customer = NewPhone();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        using var received = await WhatsAppAttentionJourney.WebhookAsync(anonymous, Inbound(
            (numberA, customer, "wamid.a." + Guid.NewGuid().ToString("N"), "Hola A"),
            (numberB, customer, "wamid.b." + Guid.NewGuid().ToString("N"), "Hola B")), Ct);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await ProcessAsync(host);

        var a = await ChatsAsync(host, tenantA);
        var b = await ChatsAsync(host, tenantB);
        Assert.Equal(["Hola A"], a.Select(value => value.Text));
        Assert.Equal(["Hola B"], b.Select(value => value.Text));
        Assert.All(a.Concat(b), value => Assert.Equal(WhatsAppInbox.Customers, value.Inbox));
        await using var scope = host.Services.CreateAsyncScope();
        var global = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
            .Where(value => value.Wamid.StartsWith("wamid.a.") || value.Wamid.StartsWith("wamid.b.")).ToArrayAsync(Ct);
        Assert.Contains(global, value => value.ProcessedAtUtc is not null);
        Assert.All(global.Where(value => value.ProcessedAtUtc is not null), value => Assert.Null(value.EncryptedText));
    }

    [Fact]
    public async Task An_active_member_writes_to_internal_and_after_leaving_to_customers()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, number, Ct)).Dispose();
        var phone = NewPhone();
        var employee = await VerifiedPhoneAccountAsync(phone);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenant, employee, Ct);
        using var anonymous = WhatsAppAttentionJourney.Client(host);

        (await WhatsAppAttentionJourney.WebhookAsync(anonymous, Inbound((number, phone, "wamid.e." + Guid.NewGuid().ToString("N"), "Soy del equipo")), Ct)).Dispose();
        await ProcessAsync(host);
        var internalChat = Assert.Single(await ChatsAsync(host, tenant));
        Assert.Equal((WhatsAppInbox.Internal, employee), (internalChat.Inbox, internalChat.MemberUserId));

        await DeactivateAsync(tenant, employee);
        (await WhatsAppAttentionJourney.WebhookAsync(anonymous, Inbound((number, phone, "wamid.e." + Guid.NewGuid().ToString("N"), "Ya no trabajo acá")), Ct)).Dispose();
        await ProcessAsync(host);
        var customerChat = Assert.Single(await ChatsAsync(host, tenant), value => value.Inbox == WhatsAppInbox.Customers);
        Assert.Null(customerChat.MemberUserId);
        Assert.NotNull(customerChat.CustomerId);
        Assert.Equal("Ya no trabajo acá", customerChat.Text);
    }

    [Fact]
    public async Task With_the_module_off_the_entry_is_kept_without_processing_or_spending_attempts()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, number, Ct)).Dispose();
        await DisableAsync(host, tenant);
        var wamid = "wamid.off." + Guid.NewGuid().ToString("N");
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        (await WhatsAppAttentionJourney.WebhookAsync(anonymous, Inbound((number, NewPhone(), wamid, "Hola")), Ct)).Dispose();
        await ProcessAsync(host);

        Assert.Empty(await ChatsAsync(host, tenant));
        await using var scope = host.Services.CreateAsyncScope();
        var kept = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
            .SingleAsync(value => value.Wamid == wamid, Ct);
        Assert.Null(kept.ProcessedAtUtc);
        Assert.NotNull(kept.EncryptedText);
        Assert.Equal(0, kept.Attempts);
        Assert.True(kept.NextAttemptAtUtc > TimeProvider.System.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task A_contact_without_a_name_takes_its_profile_name_and_the_name_does_not_stay_in_the_queue()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, number, Ct)).Dispose();
        var phone = NewPhone(); var wamid = "wamid.profile." + Guid.NewGuid().ToString("N");
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        (await WhatsAppAttentionJourney.WebhookAsync(anonymous, WithProfile(number, phone, wamid, "Ana  Pérez"), Ct)).Dispose();

        // En la cola, cifrado como el texto.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var queued = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
                .SingleAsync(value => value.Wamid == wamid, Ct);
            Assert.NotNull(queued.EncryptedProfileName);
            Assert.NotEqual("Ana Pérez", queued.EncryptedProfileName);
            Assert.Equal("Ana Pérez", scope.ServiceProvider.GetRequiredService<IPayloadProtector>().Unprotect(queued.EncryptedProfileName));
        }
        await ProcessAsync(host);

        var customer = await CustomerAsync(host, tenant, phone);
        Assert.Equal("Ana Pérez", customer.Name);
        Assert.NotNull(customer.ProfileNameTakenAtUtc);
        await using var read = host.Services.CreateAsyncScope();
        var processed = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
            .SingleAsync(value => value.Wamid == wamid, Ct);
        Assert.NotNull(processed.ProcessedAtUtc);
        Assert.Null(processed.EncryptedProfileName);
    }

    [Fact]
    public async Task A_name_set_by_a_person_is_not_replaced_and_one_they_emptied_is_not_filled_again()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, number, Ct)).Dispose();
        var phone = NewPhone();
        await InTenantAsync(host, tenant, (db, _) =>
        {
            var customer = WhatsAppCustomer.Create(PhoneNumber.Create(phone).Value, TimeProvider.System.GetUtcNow().UtcDateTime);
            customer.Rename("Ana de Ventas");
            db.Add(customer);
            return Task.CompletedTask;
        });
        using var anonymous = WhatsAppAttentionJourney.Client(host);

        (await WhatsAppAttentionJourney.WebhookAsync(anonymous, WithProfile(number, phone, "wamid.kept." + Guid.NewGuid().ToString("N"), "Anita"), Ct)).Dispose();
        await ProcessAsync(host);
        var kept = await CustomerAsync(host, tenant, phone);
        Assert.Equal("Ana de Ventas", kept.Name);
        Assert.NotNull(kept.ProfileNameTakenAtUtc);

        await InTenantAsync(host, tenant, async (db, ct) => (await db.Set<WhatsAppCustomer>().SingleAsync(value => value.WaId == phone, ct)).Rename(null));
        (await WhatsAppAttentionJourney.WebhookAsync(anonymous, WithProfile(number, phone, "wamid.empty." + Guid.NewGuid().ToString("N"), "Anita"), Ct)).Dispose();
        await ProcessAsync(host);
        var emptied = await CustomerAsync(host, tenant, phone);
        Assert.Null(emptied.Name);
        Assert.Equal(kept.ProfileNameTakenAtUtc, emptied.ProfileNameTakenAtUtc);
    }

    [Fact]
    public async Task Phone_echo_answers_the_chat_stops_waiting_notice_keeps_assignment_and_window_and_deduplicates()
    {
        const string customers = "/api/organization/whatsapp/customers";
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (ownerAccount, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await ConnectCoexistenceAsync(owner, number))
            Assert.Equal(HttpStatusCode.Created, connected.StatusCode);

        var ownerMemberId = (await JsonAsync(await owner.GetAsync("/api/organization/whatsapp/teams/member-options", Ct)))
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        var teamId = (await JsonAsync(await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/teams",
            new { name = "Ventas " + Guid.NewGuid().ToString("N")[..6], inbox = "Customers", memberIds = new[] { ownerMemberId }, notifyAfterMinutes = 10 }, Ct)))
            .GetProperty("id").GetGuid();

        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var waitingCustomer = NewPhone();
        var takenCustomer = NewPhone();
        var longAgo = TimeProvider.System.GetUtcNow().AddMinutes(-15);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, number, waitingCustomer, "Necesito presupuesto", Ct, longAgo);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, number, takenCustomer, "Hola, consulta", Ct, longAgo);

        var list = await JsonAsync(await owner.GetAsync($"{customers}/conversations", Ct));
        var waitingRow = ConversationRow(list, waitingCustomer);
        var takenRow = ConversationRow(list, takenCustomer);
        var waitingChatId = waitingRow.GetProperty("id").GetGuid();
        var takenChatId = takenRow.GetProperty("id").GetGuid();
        var waitingWindowBefore = waitingRow.GetProperty("windowOpenUntilUtc").GetDateTime();
        var takenWindowBefore = takenRow.GetProperty("windowOpenUntilUtc").GetDateTime();

        using (var transferA = await WhatsAppAttentionJourney.PostAsync(owner, $"{customers}/conversations/{waitingChatId}/transfer",
            new { teamId, version = waitingRow.GetProperty("version").GetUInt32() }, Ct))
            Assert.Equal(HttpStatusCode.OK, transferA.StatusCode);
        using (var transferB = await WhatsAppAttentionJourney.PostAsync(owner, $"{customers}/conversations/{takenChatId}/transfer",
            new { teamId, version = takenRow.GetProperty("version").GetUInt32() }, Ct))
            Assert.Equal(HttpStatusCode.OK, transferB.StatusCode);
        using (var takeB = await owner.PostAsync($"{customers}/conversations/{takenChatId}/take", null, Ct))
            Assert.Equal(HttpStatusCode.OK, takeB.StatusCode);

        var outboxBefore = await WhatsAppOutboxCountAsync(host);
        var echoWamid = "wamid.echo.text." + Guid.NewGuid().ToString("N");
        var mediaWamid = "wamid.echo.img." + Guid.NewGuid().ToString("N");
        var takenEchoWamid = "wamid.echo.taken." + Guid.NewGuid().ToString("N");
        var echoAt = TimeProvider.System.GetUtcNow().AddMinutes(-1);

        // El mismo eco dos veces en el mismo lote y otra vez en un segundo webhook: queda una sola fila.
        var echoBody = EchoWebhook(number,
            TextEcho(waitingCustomer, echoWamid, "Te lo enviamos hoy", echoAt),
            TextEcho(waitingCustomer, echoWamid, "Te lo enviamos hoy", echoAt),
            MediaEcho(waitingCustomer, mediaWamid, "image", echoAt.AddSeconds(5)),
            TextEcho(takenCustomer, takenEchoWamid, "Seguimos por acá", echoAt.AddSeconds(10)));
        using (var first = await WhatsAppAttentionJourney.WebhookAsync(anonymous, echoBody, Ct))
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using (var duplicate = await WhatsAppAttentionJourney.WebhookAsync(anonymous, echoBody, Ct))
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        await ProcessAsync(host);

        // Ningún mensaje salió al outbox (ni el eco ni el bot).
        Assert.Equal(outboxBefore, await WhatsAppOutboxCountAsync(host));

        // El chat sin tomar queda respondido, frena «Chats esperando», conserva equipo y ventana sin cambios.
        await host.Services.GetServices<IHostedService>().OfType<WhatsAppWaitingNoticeWorker>().Single().RunOnceAsync(Ct);
        Assert.Empty(await WaitingEmailsAsync(host, ownerAccount.Email.Value));

        var waitingDetail = await JsonAsync(await owner.GetAsync($"{customers}/conversations/{waitingChatId}", Ct));
        var waitingAfter = waitingDetail.GetProperty("conversation");
        Assert.False(waitingAfter.GetProperty("unread").GetBoolean());
        Assert.Equal(waitingWindowBefore, waitingAfter.GetProperty("windowOpenUntilUtc").GetDateTime());
        Assert.Equal(teamId, waitingAfter.GetProperty("team").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, waitingAfter.GetProperty("assignee").ValueKind);

        var waitingMessages = waitingDetail.GetProperty("messages").GetProperty("items").EnumerateArray().ToArray();
        var textEcho = Assert.Single(waitingMessages, item => item.GetProperty("text").GetString() == "Te lo enviamos hoy");
        Assert.Equal(("Text", "Outbound", "Sent", true),
            (textEcho.GetProperty("kind").GetString(), textEcho.GetProperty("direction").GetString(),
                textEcho.GetProperty("status").GetString(), textEcho.GetProperty("sentFromPhone").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, textEcho.GetProperty("author").ValueKind);
        var mediaEcho = Assert.Single(waitingMessages, item => item.GetProperty("kind").GetString() == "Media");
        Assert.Equal(("Image", "Outbound", "Sent", true),
            (mediaEcho.GetProperty("mediaKind").GetString(), mediaEcho.GetProperty("direction").GetString(),
                mediaEcho.GetProperty("status").GetString(), mediaEcho.GetProperty("sentFromPhone").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, mediaEcho.GetProperty("text").ValueKind);
        Assert.Equal(JsonValueKind.Null, mediaEcho.GetProperty("author").ValueKind);
        var inboundOriginal = Assert.Single(waitingMessages, item => item.GetProperty("direction").GetString() == "Inbound");
        Assert.False(inboundOriginal.GetProperty("sentFromPhone").GetBoolean());

        // El chat tomado conserva a quien lo tomó, su equipo y su ventana.
        var takenDetail = await JsonAsync(await owner.GetAsync($"{customers}/conversations/{takenChatId}", Ct));
        var takenAfter = takenDetail.GetProperty("conversation");
        Assert.False(takenAfter.GetProperty("unread").GetBoolean());
        Assert.Equal(takenWindowBefore, takenAfter.GetProperty("windowOpenUntilUtc").GetDateTime());
        Assert.Equal(teamId, takenAfter.GetProperty("team").GetProperty("id").GetGuid());
        Assert.Equal(ownerMemberId, takenAfter.GetProperty("assignee").GetProperty("id").GetGuid());
        var takenEcho = Assert.Single(takenDetail.GetProperty("messages").GetProperty("items").EnumerateArray(),
            item => item.GetProperty("text").GetString() == "Seguimos por acá");
        Assert.True(takenEcho.GetProperty("sentFromPhone").GetBoolean());

        // En la base: el texto queda cifrado (ADR 0065), sin OutboxMessageId, y la cola global purgada.
        var echoMessageId = textEcho.GetProperty("id").GetGuid();
        var rawTextBytes = await RawChatMessageTextAsync(tenant, echoMessageId);
        Assert.NotNull(rawTextBytes);
        Assert.NotEqual(Encoding.UTF8.GetBytes("Te lo enviamos hoy"), rawTextBytes);
        await using var scope = host.Services.CreateAsyncScope();
        var queued = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
            .Where(value => value.Wamid == echoWamid || value.Wamid == mediaWamid || value.Wamid == takenEchoWamid).ToArrayAsync(Ct);
        Assert.Equal(3, queued.Length);
        Assert.All(queued, row =>
        {
            Assert.NotNull(row.ProcessedAtUtc);
            Assert.Null(row.EncryptedText);
            Assert.Null(row.ContactId);
        });
    }

    [Fact]
    public async Task Phone_echo_to_new_customer_creates_contact_and_chat_with_closed_window_and_no_bot()
    {
        const string customers = "/api/organization/whatsapp/customers";
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await ConnectCoexistenceAsync(owner, number))
            Assert.Equal(HttpStatusCode.Created, connected.StatusCode);

        var outboxBefore = await WhatsAppOutboxCountAsync(host);
        var newCustomer = NewPhone();
        var wamid = "wamid.echo.new." + Guid.NewGuid().ToString("N");
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        using (var received = await WhatsAppAttentionJourney.WebhookAsync(anonymous,
            EchoWebhook(number, TextEcho(newCustomer, wamid, "Hola, te escribo desde el negocio", TimeProvider.System.GetUtcNow())), Ct))
            Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await ProcessAsync(host);

        Assert.Equal(outboxBefore, await WhatsAppOutboxCountAsync(host));
        var customer = await CustomerAsync(host, tenant, newCustomer);
        Assert.Equal(newCustomer, customer.WaId);

        var list = await JsonAsync(await owner.GetAsync($"{customers}/conversations", Ct));
        var row = ConversationRow(list, newCustomer);
        Assert.False(row.GetProperty("unread").GetBoolean());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("windowOpenUntilUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("assignee").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("team").ValueKind);

        var chatId = row.GetProperty("id").GetGuid();
        var detail = await JsonAsync(await owner.GetAsync($"{customers}/conversations/{chatId}", Ct));
        var message = Assert.Single(detail.GetProperty("messages").GetProperty("items").EnumerateArray());
        Assert.Equal(("Text", "Outbound", "Sent", true, "Hola, te escribo desde el negocio"),
            (message.GetProperty("kind").GetString(), message.GetProperty("direction").GetString(),
                message.GetProperty("status").GetString(), message.GetProperty("sentFromPhone").GetBoolean(),
                message.GetProperty("text").GetString()));
        Assert.Equal(JsonValueKind.Null, message.GetProperty("author").ValueKind);

        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
        var storedChat = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChat>().AsNoTracking()
            .SingleAsync(value => value.Id == chatId, Ct);
        Assert.Null(storedChat.LastInboundAtUtc);
        Assert.Null(storedChat.WaitingSinceUtc);
    }

    [Fact]
    public async Task History_webhook_is_ignored_and_echo_only_enters_its_own_channel_and_organization()
    {
        const string customers = "/api/organization/whatsapp/customers";
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var first = WhatsAppAttentionJourney.Client(host);
        using var second = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, first, Ct);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, second, Ct);
        var numberA = WhatsAppAttentionJourney.NewNumberId();
        var numberB = WhatsAppAttentionJourney.NewNumberId();
        using (var c1 = await ConnectCoexistenceAsync(first, numberA)) Assert.Equal(HttpStatusCode.Created, c1.StatusCode);
        using (var c2 = await ConnectCoexistenceAsync(second, numberB)) Assert.Equal(HttpStatusCode.Created, c2.StatusCode);

        var customer = NewPhone();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, numberA, customer, "Hola organización A", Ct);

        // El historial previo a la conexión («history») se ignora.
        using (var history = await WhatsAppAttentionJourney.WebhookAsync(anonymous, HistoryWebhook(numberA, customer), Ct))
            Assert.Equal(HttpStatusCode.OK, history.StatusCode);

        // Un eco del número de la organización B y uno de un número desconocido nunca entran al chat de la organización A.
        using (var echoB = await WhatsAppAttentionJourney.WebhookAsync(anonymous,
            EchoWebhook(numberB, TextEcho(customer, "wamid.echo.b." + Guid.NewGuid().ToString("N"), "Respuesta de B", TimeProvider.System.GetUtcNow())), Ct))
            Assert.Equal(HttpStatusCode.OK, echoB.StatusCode);
        using (var echoUnknown = await WhatsAppAttentionJourney.WebhookAsync(anonymous,
            EchoWebhook("pn-unknown-" + Guid.NewGuid().ToString("N")[..8],
                TextEcho(customer, "wamid.echo.u." + Guid.NewGuid().ToString("N"), "De otro canal", TimeProvider.System.GetUtcNow())), Ct))
            Assert.Equal(HttpStatusCode.OK, echoUnknown.StatusCode);
        await ProcessAsync(host);

        var listA = await JsonAsync(await first.GetAsync($"{customers}/conversations", Ct));
        var rowA = ConversationRow(listA, customer);
        Assert.True(rowA.GetProperty("unread").GetBoolean());
        var detailA = await JsonAsync(await first.GetAsync($"{customers}/conversations/{rowA.GetProperty("id").GetGuid()}", Ct));
        var messageA = Assert.Single(detailA.GetProperty("messages").GetProperty("items").EnumerateArray(),
            item => item.GetProperty("direction").GetString() == "Inbound" || item.GetProperty("sentFromPhone").GetBoolean());
        Assert.Equal(("Inbound", "Hola organización A", false),
            (messageA.GetProperty("direction").GetString(), messageA.GetProperty("text").GetString(), messageA.GetProperty("sentFromPhone").GetBoolean()));

        var listB = await JsonAsync(await second.GetAsync($"{customers}/conversations", Ct));
        var rowB = ConversationRow(listB, customer);
        var detailB = await JsonAsync(await second.GetAsync($"{customers}/conversations/{rowB.GetProperty("id").GetGuid()}", Ct));
        var messageB = Assert.Single(detailB.GetProperty("messages").GetProperty("items").EnumerateArray());
        Assert.Equal(("Outbound", "Respuesta de B", true),
            (messageB.GetProperty("direction").GetString(), messageB.GetProperty("text").GetString(), messageB.GetProperty("sentFromPhone").GetBoolean()));
    }

    private static async Task<WhatsAppCustomer> CustomerAsync(WebApplicationFactory<Program> host, Guid tenantId, string phone)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppCustomer>().AsNoTracking()
            .SingleAsync(value => value.WaId == phone, Ct);
    }

    /// <summary>Lo que hace una persona desde Clientes, dentro del alcance de su organización.</summary>
    private static async Task InTenantAsync(WebApplicationFactory<Program> host, Guid tenantId, Func<ApplicationDbContext, CancellationToken, Task> work)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        { await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), ct); return Result.Success(); }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task ProcessAsync(WebApplicationFactory<Program> host) =>
        await host.Services.GetServices<IHostedService>().OfType<WhatsAppInboundProcessor>().Single().ProcessPendingAsync(Ct);

    private sealed record ChatRow(WhatsAppInbox Inbox, Guid? MemberUserId, Guid? CustomerId, string? Text);

    /// <summary>Lo que escribió la otra parte, como lo ve la organización: entra a su alcance y lee con RLS (el bot responde aparte, B3).</summary>
    private static async Task<IReadOnlyList<ChatRow>> ChatsAsync(WebApplicationFactory<Program> host, Guid tenantId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await (from chat in db.Set<WhatsAppChat>().AsNoTracking()
                      from message in db.Set<WhatsAppChatMessage>().AsNoTracking()
                          .Where(value => value.ChatId == chat.Id && value.Direction == WhatsAppMessageDirection.Inbound)
                      orderby message.CreatedAtUtc
                      select new ChatRow(chat.Inbox, chat.MemberUserId, chat.CustomerId, message.Text)).ToArrayAsync(Ct);
    }

    private async Task<Guid> VerifiedPhoneAccountAsync(string phone)
    {
        var user = Guid.Empty;
        await TransactionAsync(async (provider, ct) =>
        {
            user = (await provider.GetRequiredService<IUserRepository>().CreateAsync("Empleada", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = LoginMethod.CreatePhone(user, PhoneNumber.Create(phone).Value);
            method.Verify(provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            provider.GetRequiredService<ILoginMethodRepository>().Add(method);
        });
        return user;
    }

    private async Task DeactivateAsync(Guid tenantId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var member = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Members.SingleAsync(value => value.UserId == userId, ct);
            Assert.True(member.Deactivate().IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    /// <summary>Apaga el módulo e invalida la caché del host que procesa (cada host tiene la suya).</summary>
    private static async Task DisableAsync(WebApplicationFactory<Program> host, Guid tenantId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var feature = await services.GetRequiredService<ApplicationDbContext>().TenantFeatures
                .SingleAsync(value => value.TenantId == tenantId && value.FeatureKey == WhatsAppFeature.Key, ct);
            Assert.True(feature.Update(false, feature.Source, null).IsSuccess);
            services.GetRequiredService<ISecurityEventRepository>().Add(Domain.Auditing.SecurityEvent.ForTenantFeatures(
                Domain.Auditing.AuditActorKind.System, Guid.Empty, tenantId, "WhatsApp attention fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task TransactionAsync(Func<IServiceProvider, CancellationToken, Task> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        { await work(scope.ServiceProvider, ct); return Result.Success(); }, CommitPolicy.OnSuccess, Ct);
    }

    private static string NewPhone() => "+54911" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(30_000_000, 59_999_999)
        .ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Un mensaje con el perfil del remitente en contacts[], como lo manda Meta.</summary>
    private static string WithProfile(string phoneNumberId, string from, string wamid, string profileName) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[] { new
        {
            id = "waba-" + phoneNumberId,
            changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
                contacts = new[] { new { profile = new { name = profileName }, wa_id = from.TrimStart('+') } },
                messages = new[] { new
                {
                    from = from.TrimStart('+'), id = wamid, type = "text", text = new { body = "Hola" },
                    timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                } },
            } } },
        } },
    });

    private static string Inbound(params (string PhoneNumberId, string From, string Wamid, string Text)[] messages) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = messages.GroupBy(value => value.PhoneNumberId).Select(group => new
        {
            id = "waba-" + group.Key,
            changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = group.Key },
                messages = group.Select(value => new
                {
                    from = value.From.TrimStart('+'), id = value.Wamid, type = "text", text = new { body = value.Text },
                    timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                }).ToArray(),
            } } },
        }).ToArray(),
    });

    private static Task<HttpResponseMessage> ConnectCoexistenceAsync(HttpClient client, string phoneNumberId) =>
        WhatsAppAttentionJourney.PostAsync(client, "/api/organization/whatsapp/numbers",
            new { code = "signup-code-" + phoneNumberId, phoneNumberId, wabaId = "waba-" + phoneNumberId, companyId = (Guid?)null, coexistence = true }, Ct);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using (response) return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private static JsonElement ConversationRow(JsonElement list, string waId) =>
        list.GetProperty("conversations").GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("displayNumber").GetString() == waId);

    private static async Task<int> WhatsAppOutboxCountAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.CountAsync(value => value.Channel == "whatsapp", Ct);
    }

    private static async Task<IReadOnlyList<EmailMessage>> WaitingEmailsAsync(WebApplicationFactory<Program> host, string address)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var protector = scope.ServiceProvider.GetRequiredService<IPayloadProtector>();
        var rows = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(value => value.Channel == "email").ToArrayAsync(Ct);
        var messages = new List<EmailMessage>();
        foreach (var row in rows)
        {
            try { messages.Add(JsonSerializer.Deserialize<EmailMessage>(protector.Unprotect(row.EncryptedPayload))!); }
            catch (System.Security.Cryptography.CryptographicException) { }
        }
        return [.. messages.Where(message => message.To == address && message.Subject.Contains("esperando", StringComparison.Ordinal))];
    }

    private async Task<byte[]?> RawChatMessageTextAsync(Guid tenantId, Guid messageId)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using (var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", connection))
        {
            enter.Parameters.AddWithValue("tenant", tenantId.ToString());
            await enter.ExecuteNonQueryAsync(Ct);
        }
        await using var command = new NpgsqlCommand("SELECT \"Text\" FROM tenant.\"WhatsAppChatMessages\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", messageId);
        return await command.ExecuteScalarAsync(Ct) as byte[];
    }

    /// <summary>Formato oficial de Meta para <c>smb_message_echoes</c> en coexistencia.</summary>
    private static string EchoWebhook(string phoneNumberId, params object[] echoes) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[] { new
        {
            id = "waba-" + phoneNumberId,
            changes = new[] { new { field = "smb_message_echoes", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
                message_echoes = echoes,
            } } },
        } },
    });

    private static object TextEcho(string to, string wamid, string body, DateTimeOffset at) => new
    {
        from = "5491100000000",
        to = to.TrimStart('+'),
        id = wamid,
        timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        type = "text",
        text = new { body },
    };

    private static object MediaEcho(string to, string wamid, string type, DateTimeOffset at) => new
    {
        from = "5491100000000",
        to = to.TrimStart('+'),
        id = wamid,
        timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        type,
        image = new { id = "media-" + wamid, mime_type = "image/jpeg" },
    };

    private static string HistoryWebhook(string phoneNumberId, string customer) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[] { new
        {
            id = "waba-" + phoneNumberId,
            changes = new[] { new { field = "history", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
                history = new[] { new
                {
                    metadata = new { phase = 0, chunk_order = 1, progress = 100 },
                    threads = new[] { new
                    {
                        id = customer.TrimStart('+'),
                        messages = new[] { new
                        {
                            from = "5491100000000",
                            to = customer.TrimStart('+'),
                            id = "wamid.history." + Guid.NewGuid().ToString("N"),
                            timestamp = TimeProvider.System.GetUtcNow().AddDays(-10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                            type = "text",
                            text = new { body = "Historial viejo que no debe entrar" },
                        } },
                    } },
                } },
            } } },
        } },
    });
}
