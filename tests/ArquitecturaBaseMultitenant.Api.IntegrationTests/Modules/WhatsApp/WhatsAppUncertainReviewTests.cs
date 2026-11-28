using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B7 (ADR 0066 y decisión 255) en las bandejas de la organización: lo que la cola dejó «Sin confirmar» se lee así hasta que
/// Meta avise otra cosa; lo revisa cualquiera que puede enviar en la bandeja, una vez, con quién y cuándo; lo ajeno es 404 y
/// sin permiso, 403. La cola de WhatsApp ordena por chat y lo que contesta sale antes que una difusión.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppUncertainReviewTests(ApiFactory factory)
{
    private const string Customers = "/api/organization/whatsapp/customers";
    private const string Internal = "/api/organization/whatsapp/internal";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Customers_inbox_reads_unconfirmed_from_the_queue_and_reviews_it_once()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct)).Dispose();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var (customer, other) = (WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone());
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, "Hola", Ct);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, other, "Buenas", Ct);
        var list = await JsonAsync(owner.GetAsync($"{Customers}/conversations", Ct));
        var (chat, otherChat) = (ChatId(list, customer), ChatId(list, other));

        var first = await SendAsync(owner, $"{Customers}/conversations/{chat}/messages", "Te ayudo");
        var second = await SendAsync(owner, $"{Customers}/conversations/{chat}/messages", "¿Seguís ahí?");
        var note = await SendAsync(owner, $"{Customers}/conversations/{chat}/notes", "Nota interna");
        var elsewhere = await SendAsync(owner, $"{Customers}/conversations/{otherChat}/messages", "Hola");
        var inbound = Messages(await JsonAsync(owner.GetAsync($"{Customers}/conversations/{chat}", Ct)))
            .Single(message => message.GetProperty("direction").GetString() == "Inbound").GetProperty("id").GetGuid();

        // Lo que contesta el operador va como respuesta y detrás de lo anterior del mismo chat; otro chat, por su lado.
        var (firstRow, secondRow, elsewhereRow) = (await QueuedAsync(host, tenant, first), await QueuedAsync(host, tenant, second),
            await QueuedAsync(host, tenant, elsewhere));
        Assert.Equal([OutboxPriority.Reply, OutboxPriority.Reply], new[] { firstRow.Priority, secondRow.Priority });
        Assert.NotNull(firstRow.OrderingKey);
        Assert.Equal(firstRow.OrderingKey, secondRow.OrderingKey);
        Assert.NotEqual(firstRow.OrderingKey, elsewhereRow.OrderingKey);
        Assert.DoesNotContain(customer.TrimStart('+'), firstRow.OrderingKey, StringComparison.Ordinal);

        await OutboxRows.SetStatusAsync(host.Services, firstRow.Id, OutboxStatus.Uncertain, Ct);
        await OutboxRows.SetStatusAsync(host.Services, elsewhereRow.Id, OutboxStatus.Uncertain, Ct);
        var unconfirmed = Message(await JsonAsync(owner.GetAsync($"{Customers}/conversations/{chat}", Ct)), first);
        Assert.Equal(("Uncertain", JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null), (unconfirmed.GetProperty("status").GetString(),
            unconfirmed.GetProperty("failureReason").ValueKind, unconfirmed.GetProperty("reviewedByName").ValueKind,
            unconfirmed.GetProperty("reviewedAtUtc").ValueKind));
        Assert.Equal("Pending", Message(await JsonAsync(owner.GetAsync($"{Customers}/conversations/{chat}", Ct)), second)
            .GetProperty("status").GetString());

        // Sin el permiso de la bandeja, 403; en otra bandeja, en otro chat o en otra organización, 404.
        using var internalOnly = await AgentAsync(host, tenant, Permissions.WhatsApp.Internal);
        await ProblemAsync(internalOnly.PostAsync(Review(Customers, chat, first), null, Ct), HttpStatusCode.Forbidden, null);
        await ProblemAsync(owner.PostAsync(Review(Internal, chat, first), null, Ct), HttpStatusCode.NotFound, WhatsAppErrors.ConversationNotFoundCode);
        await ProblemAsync(owner.PostAsync(Review(Customers, otherChat, first), null, Ct), HttpStatusCode.NotFound, WhatsAppErrors.ConversationNotFoundCode);
        await ProblemAsync(owner.PostAsync(Review(Customers, chat, Guid.CreateVersion7()), null, Ct), HttpStatusCode.NotFound,
            WhatsAppErrors.ConversationNotFoundCode);
        using (var stranger = WhatsAppAttentionJourney.Client(host))
        {
            await WhatsAppAttentionJourney.OrganizationAsync(factory, host, stranger, Ct);
            await ProblemAsync(stranger.PostAsync(Review(Customers, chat, first), null, Ct), HttpStatusCode.NotFound,
                WhatsAppErrors.ConversationNotFoundCode);
        }

        // Lo que no quedó «Sin confirmar» no se revisa: lo pendiente, una nota ni lo recibido.
        foreach (var id in new[] { second, note, inbound })
            await ProblemAsync(owner.PostAsync(Review(Customers, chat, id), null, Ct), HttpStatusCode.Conflict, WhatsAppErrors.MessageNotUncertainCode);

        // Revisa cualquiera que puede enviar, sin tomar el chat; la segunda revisión devuelve la primera.
        using var agent = await AgentAsync(host, tenant, Permissions.WhatsApp.Customers);
        var reviewed = await JsonAsync(agent.PostAsync(Review(Customers, chat, first), null, Ct));
        Assert.Equal(("Uncertain", first), (reviewed.GetProperty("status").GetString(), reviewed.GetProperty("id").GetGuid()));
        var reviewer = reviewed.GetProperty("reviewedByName").GetString();
        var reviewedAt = reviewed.GetProperty("reviewedAtUtc").GetDateTime();
        Assert.Equal("Ana Gómez", reviewer);
        var again = await JsonAsync(owner.PostAsync(Review(Customers, chat, first), null, Ct));
        Assert.Equal((reviewer, reviewedAt), (again.GetProperty("reviewedByName").GetString(), again.GetProperty("reviewedAtUtc").GetDateTime()));
        var listed = Message(await JsonAsync(owner.GetAsync($"{Customers}/conversations/{chat}", Ct)), first);
        Assert.Equal((reviewer, reviewedAt), (listed.GetProperty("reviewedByName").GetString(), listed.GetProperty("reviewedAtUtc").GetDateTime()));

        // Un aviso de Meta posterior manda sobre la cola: ya no está «Sin confirmar» y no hay nada que revisar.
        await AcceptedAsync(host, phoneNumberId, elsewhereRow.Id, WhatsAppMessageStatus.Delivered);
        Assert.Equal("Delivered", Message(await JsonAsync(owner.GetAsync($"{Customers}/conversations/{otherChat}", Ct)), elsewhere)
            .GetProperty("status").GetString());
        await ProblemAsync(owner.PostAsync(Review(Customers, otherChat, elsewhere), null, Ct), HttpStatusCode.Conflict,
            WhatsAppErrors.MessageNotUncertainCode);
    }

    /// <summary>
    /// Dos que pueden enviar marcan como revisado el mismo envío «Sin confirmar» a la vez: la primera revisión queda escrita y sin
    /// confirmar cuando llega la segunda. El lock de la conversación hace esperar a la segunda, que lee la primera y la devuelve;
    /// sin el lock, la segunda la pisaría con otro nombre y otra hora.
    /// </summary>
    [Fact]
    public async Task Two_agents_reviewing_the_same_unconfirmed_message_at_once_both_get_the_first_review()
    {
        var hold = new CommitHold(context => context.ChangeTracker.Entries<WhatsAppChatMessage>()
            .Any(entry => entry.Entity.ReviewedAtUtc is not null));
        await using var attention = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        await using var host = attention.WithWebHostBuilder(hold.Install);
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (account, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        // Otro nombre que el de la agente: una segunda revisión que pisara la primera se vería en la respuesta.
        await using (var naming = host.Services.CreateAsyncScope())
            await naming.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.Where(user => user.Id == account.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DisplayName, "Bruno Díaz"), Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct)).Dispose();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var customer = WhatsAppAttentionJourney.NewPhone();
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, "Hola", Ct);
        var chat = ChatId(await JsonAsync(owner.GetAsync($"{Customers}/conversations", Ct)), customer);
        var sent = await SendAsync(owner, $"{Customers}/conversations/{chat}/messages", "Te ayudo");
        await OutboxRows.SetStatusAsync(host.Services, (await QueuedAsync(host, tenant, sent)).Id, OutboxStatus.Uncertain, Ct);
        using var agent = await AgentAsync(host, tenant, Permissions.WhatsApp.Customers);

        var (first, second) = await hold.RaceAsync(host.Services, () => agent.PostAsync(Review(Customers, chat, sent), null, Ct),
            () => owner.PostAsync(Review(Customers, chat, sent), null, Ct), Ct);

        var (byAgent, byOwner) = (await JsonAsync(first), await JsonAsync(second));
        Assert.Equal("Ana Gómez", byAgent.GetProperty("reviewedByName").GetString());
        Assert.Equal((byAgent.GetProperty("reviewedByName").GetString(), byAgent.GetProperty("reviewedAtUtc").GetDateTime()),
            (byOwner.GetProperty("reviewedByName").GetString(), byOwner.GetProperty("reviewedAtUtc").GetDateTime()));
        var listed = Message(await JsonAsync(owner.GetAsync($"{Customers}/conversations/{chat}", Ct)), sent);
        Assert.Equal("Ana Gómez", listed.GetProperty("reviewedByName").GetString());
    }

    [Fact]
    public async Task Internal_inbox_reviews_with_its_own_permission()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (account, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        Guid channelId;
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct))
            channelId = (await WhatsAppAttentionJourney.JsonAsync(connected, Ct)).GetProperty("id").GetGuid();
        var (chat, unconfirmed, pending) = await InternalChatAsync(host, tenant, account.UserId, channelId, phoneNumberId);
        await OutboxRows.SetStatusAsync(host.Services, await OutboxIdAsync(host, tenant, unconfirmed), OutboxStatus.Uncertain, Ct);

        using var customersOnly = await AgentAsync(host, tenant, Permissions.WhatsApp.Customers);
        await ProblemAsync(customersOnly.PostAsync(Review(Internal, chat, unconfirmed), null, Ct), HttpStatusCode.Forbidden, null);
        await ProblemAsync(owner.PostAsync(Review(Customers, chat, unconfirmed), null, Ct), HttpStatusCode.NotFound, WhatsAppErrors.ConversationNotFoundCode);
        await ProblemAsync(owner.PostAsync(Review(Internal, chat, pending), null, Ct), HttpStatusCode.Conflict, WhatsAppErrors.MessageNotUncertainCode);

        using var agent = await AgentAsync(host, tenant, Permissions.WhatsApp.Internal);
        var reviewed = await JsonAsync(agent.PostAsync(Review(Internal, chat, unconfirmed), null, Ct));
        var again = await JsonAsync(owner.PostAsync(Review(Internal, chat, unconfirmed), null, Ct));
        Assert.Equal(("Uncertain", reviewed.GetProperty("reviewedByName").GetString(), reviewed.GetProperty("reviewedAtUtc").GetDateTime()),
            (again.GetProperty("status").GetString(), again.GetProperty("reviewedByName").GetString(), again.GetProperty("reviewedAtUtc").GetDateTime()));
        Assert.Equal(reviewed.GetProperty("reviewedByName").GetString(),
            Message(await JsonAsync(owner.GetAsync($"{Internal}/conversations/{chat}", Ct)), unconfirmed).GetProperty("reviewedByName").GetString());
    }

    /// <summary>
    /// Con las opciones que pone cada emisor de WhatsApp: un chat sale en orden aunque lo segundo sea una respuesta, y lo que
    /// contesta (el bot o el operador) sale antes que una difusión anterior.
    /// </summary>
    [Fact]
    public async Task A_chat_keeps_its_order_and_the_bot_goes_before_an_earlier_broadcast()
    {
        using var client = factory.CreateClient();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        var channel = "whatsapp-b7-" + Guid.NewGuid().ToString("N")[..8];
        var now = clock.GetUtcNow().UtcDateTime;
        WhatsAppOutboundMessage To(string phone) => new("pn-b7", phone, "", "", [], Body: "Hola", ReplyExpiresAtUtc: now.AddHours(24));
        var broadcast = await EnqueueAsync(clock, channel, To("+5491130000001").QueueOptions(OutboxPriority.Notice));
        clock.Advance(TimeSpan.FromSeconds(1));
        var firstInChat = await EnqueueAsync(clock, channel, To("+5491130000002").QueueOptions(OutboxPriority.Notice));
        clock.Advance(TimeSpan.FromSeconds(1));
        var replyInChat = await EnqueueAsync(clock, channel, To("+5491130000002").QueueOptions(OutboxPriority.Reply));
        clock.Advance(TimeSpan.FromSeconds(1));
        var bot = await EnqueueAsync(clock, channel, To("+5491130000003").QueueOptions(OutboxPriority.Reply));

        // El bot contesta antes que la difusión; la respuesta del chat espera al aviso anterior de ese mismo chat.
        var claimedBot = (await ClaimAsync(clock, channel))!;
        Assert.Equal(bot, claimedBot.MessageId);
        var claimedBroadcast = (await ClaimAsync(clock, channel))!;
        Assert.Equal(broadcast, claimedBroadcast.MessageId);
        var claimedFirst = (await ClaimAsync(clock, channel))!;
        Assert.Equal(firstInChat, claimedFirst.MessageId);
        Assert.Null(await ClaimAsync(clock, channel));
        await CompleteAsync(clock, claimedFirst, OutboxDeliveryReceipt.Accepted());
        Assert.Equal(replyInChat, (await ClaimAsync(clock, channel))!.MessageId);
    }

    private static string Review(string inbox, Guid chat, Guid message) => $"{inbox}/conversations/{chat}/messages/{message}/review";

    /// <summary>
    /// Un aviso «failed» del webhook deja su motivo en el mensaje del chat de la organización (B5). Va en el grupo de la API y no en
    /// <c>WhatsAppWebhookTests</c>: crear una organización siembra la configuración de Plataforma en la base de esa clase, y la cuota
    /// de arranque que prueba su caso del 429 dejaría de valer.
    /// </summary>
    [Fact]
    public async Task A_failed_status_webhook_records_its_reason_on_the_chat_message()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        owner.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-AR");
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct))
            Assert.Equal(HttpStatusCode.Created, connected.StatusCode);

        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var customer = WhatsAppAttentionJourney.NewPhone();
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, "Hola", Ct);
        using var listResponse = await owner.GetAsync("/api/organization/whatsapp/customers/conversations", Ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var chatId = (await WhatsAppAttentionJourney.JsonAsync(listResponse, Ct))
            .GetProperty("conversations").GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("displayNumber").GetString() == customer).GetProperty("id").GetGuid();

        Guid firstId, secondId;
        using (var firstResponse = await WhatsAppAttentionJourney.PostAsync(owner, $"/api/organization/whatsapp/customers/conversations/{chatId}/messages", new { text = "Primer aviso" }, Ct))
        {
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
            firstId = (await WhatsAppAttentionJourney.JsonAsync(firstResponse, Ct)).GetProperty("id").GetGuid();
        }
        using (var secondResponse = await WhatsAppAttentionJourney.PostAsync(owner, $"/api/organization/whatsapp/customers/conversations/{chatId}/messages", new { text = "Segundo aviso" }, Ct))
        {
            Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
            secondId = (await WhatsAppAttentionJourney.JsonAsync(secondResponse, Ct)).GetProperty("id").GetGuid();
        }

        Guid firstOutbox, secondOutbox;
        await using (var readOutbox = host.Services.CreateAsyncScope())
        {
            readOutbox.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
            var rows = await readOutbox.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChatMessage>()
                .AsNoTracking().Where(value => value.Id == firstId || value.Id == secondId).ToArrayAsync(Ct);
            firstOutbox = Assert.Single(rows, value => value.Id == firstId).OutboxMessageId!.Value;
            secondOutbox = Assert.Single(rows, value => value.Id == secondId).OutboxMessageId!.Value;
        }

        var wamidFirst = "wamid.chat.131049." + Guid.NewGuid().ToString("N");
        var wamidSecond = "wamid.chat.131026." + Guid.NewGuid().ToString("N");
        var nowUtc = host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var observer = scope.ServiceProvider.GetServices<IOutboxDeliveryObserver>().Single(value => value.Key == "whatsapp");
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await observer.RecordAcceptedAsync(firstOutbox, OutboxDeliveryReceipt.Accepted(wamidFirst, phoneNumberId, "Text"), nowUtc, ct);
                await observer.RecordAcceptedAsync(secondOutbox, OutboxDeliveryReceipt.Accepted(wamidSecond, phoneNumberId, "Text"), nowUtc, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        using (var failed = await WhatsAppAttentionJourney.WebhookAsync(anonymous, WhatsAppAttentionJourney.Statuses(phoneNumberId,
            WhatsAppAttentionJourney.Status(wamidFirst, "failed", error: 131049),
            WhatsAppAttentionJourney.Status(wamidSecond, "failed", error: 131026)), Ct))
            Assert.Equal(HttpStatusCode.OK, failed.StatusCode);

        // El webhook deja cada motivo en la cola durable, en su misma transacción; lo anota el procesador dentro del alcance
        // de la organización, así una caída después del commit no lo pierde.
        Assert.Equal(new Dictionary<string, string?> { [wamidFirst] = WhatsAppSendFailures.RateLimited, [wamidSecond] = WhatsAppSendFailures.Undeliverable },
            await WhatsAppAttentionJourney.PendingDeliveryFailuresAsync(host, [wamidFirst, wamidSecond], Ct));
        await WhatsAppAttentionJourney.ProcessInboundAsync(host, Ct);
        Assert.Empty(await WhatsAppAttentionJourney.PendingDeliveryFailuresAsync(host, [wamidFirst, wamidSecond], Ct));

        await using (var verify = host.Services.CreateAsyncScope())
        {
            verify.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
            var rows = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChatMessage>()
                .AsNoTracking().Where(value => value.Id == firstId || value.Id == secondId).ToArrayAsync(Ct);
            var firstRow = Assert.Single(rows, value => value.Id == firstId);
            var secondRow = Assert.Single(rows, value => value.Id == secondId);
            Assert.Equal((WhatsAppChatMessageStatus.Failed, WhatsAppSendFailures.RateLimited, wamidFirst),
                (firstRow.Status, firstRow.FailureReason, firstRow.Wamid));
            Assert.Equal((WhatsAppChatMessageStatus.Failed, WhatsAppSendFailures.Undeliverable, wamidSecond),
                (secondRow.Status, secondRow.FailureReason, secondRow.Wamid));
        }

        using var chatResponse = await owner.GetAsync($"/api/organization/whatsapp/customers/conversations/{chatId}", Ct);
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);
        var messages = (await WhatsAppAttentionJourney.JsonAsync(chatResponse, Ct))
            .GetProperty("messages").GetProperty("items").EnumerateArray().ToArray();
        var firstMessage = Assert.Single(messages, value => value.GetProperty("id").GetGuid() == firstId);
        var secondMessage = Assert.Single(messages, value => value.GetProperty("id").GetGuid() == secondId);
        Assert.Equal(("Failed", "WhatsApp frenó el envío por su límite de mensajes. Probá de nuevo en un rato."),
            (firstMessage.GetProperty("status").GetString(), firstMessage.GetProperty("failureReason").GetString()));
        Assert.Equal(("Failed", "WhatsApp no pudo entregarlo a ese número."),
            (secondMessage.GetProperty("status").GetString(), secondMessage.GetProperty("failureReason").GetString()));
    }

    private static async Task<Guid> SendAsync(HttpClient client, string path, string text)
    {
        using var response = await WhatsAppAttentionJourney.PostAsync(client, path, new { text }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await WhatsAppAttentionJourney.JsonAsync(response, Ct)).GetProperty("id").GetGuid();
    }

    private static Guid ChatId(JsonElement list, string waId) => list.GetProperty("conversations").GetProperty("items").EnumerateArray()
        .Single(row => row.GetProperty("displayNumber").GetString() == waId).GetProperty("id").GetGuid();

    private static JsonElement[] Messages(JsonElement conversation) =>
        [.. conversation.GetProperty("messages").GetProperty("items").EnumerateArray()];

    private static JsonElement Message(JsonElement conversation, Guid id) =>
        Messages(conversation).Single(message => message.GetProperty("id").GetGuid() == id);

    private static async Task<JsonElement> JsonAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private static async Task ProblemAsync(Task<HttpResponseMessage> request, HttpStatusCode status, string? code)
    {
        using var response = await request;
        Assert.Equal(status, response.StatusCode);
        if (code is not null) Assert.Equal(code, (await WhatsAppAttentionJourney.JsonAsync(response, Ct)).GetProperty("code").GetString());
    }

    private static async Task<OutboxMessage> QueuedAsync(WebApplicationFactory<Program> host, Guid tenant, Guid message)
    {
        var id = await OutboxIdAsync(host, tenant, message);
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == id, Ct);
    }

    private static async Task<Guid> OutboxIdAsync(WebApplicationFactory<Program> host, Guid tenant, Guid message)
    {
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChatMessage>().AsNoTracking()
            .SingleAsync(value => value.Id == message, Ct)).OutboxMessageId!.Value;
    }

    /// <summary>Lo que Meta informó del envío: la fila «accepted» con su estado.</summary>
    private static async Task AcceptedAsync(WebApplicationFactory<Program> host, string phoneNumberId, Guid outboxId, WhatsAppMessageStatus status)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(WhatsAppMessage.Create(phoneNumberId, "wamid.b7." + Guid.NewGuid().ToString("N"),
                "accepted", WhatsAppMessageDirection.Outbound, WhatsAppMessageKind.Text, status, now, now, outboxMessageId: outboxId));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    /// <summary>Un chat de Internos con dos envíos del Dueño: uno que va a quedar «Sin confirmar» y otro pendiente.</summary>
    private static async Task<(Guid Chat, Guid Unconfirmed, Guid Pending)> InternalChatAsync(WebApplicationFactory<Program> host, Guid tenant,
        Guid memberUserId, Guid channelId, string phoneNumberId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var phone = WhatsAppAttentionJourney.NewPhone();
        var chat = WhatsAppChat.ForMember(channelId, PhoneNumber.Create(phone).Value, memberUserId, now);
        var messages = new List<WhatsAppChatMessage>();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var outbox = services.GetRequiredService<IOutbox>();
            context.Add(chat);
            foreach (var text in new[] { "Hola", "¿Me confirmás?" })
            {
                var message = new WhatsAppOutboundMessage(phoneNumberId, phone, "", "", [], Body: text, ReplyExpiresAtUtc: now.AddHours(1), ChannelId: channelId);
                var outboxId = outbox.Enqueue("whatsapp", JsonSerializer.Serialize(message), options: message.QueueOptions(OutboxPriority.Reply));
                messages.Add(WhatsAppChatMessage.Outbound(chat.Id, WhatsAppChatMessageKind.Text, memberUserId, "Dueño", text, outboxId, null, now));
            }
            context.AddRange(messages);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return (chat.Id, messages[0].Id, messages[1].Id);
    }

    /// <summary>Una persona que entra como empresa y es miembro con un rol que da esos permisos.</summary>
    private async Task<HttpClient> AgentAsync(WebApplicationFactory<Program> host, Guid tenant, params string[] permissions)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await using (var naming = host.Services.CreateAsyncScope())
            await naming.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.Where(user => user.Id == account.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DisplayName, "Ana Gómez"), Ct);
        var member = await OrganizationJourney.AddActiveMemberAsync(factory, tenant, account.UserId, Ct);
        var role = Role.Create("Atención " + Guid.NewGuid().ToString("N")[..6], null, RoleScope.Organization, null, permissions).Value;
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().AddRange(role, RoleAssignment.Assign(member, role.Id, role.Scope, null, null).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        return client;
    }

    private async Task<Guid> EnqueueAsync(TimeProvider clock, string channel, OutboxEnqueueOptions options)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var outbox = new Outbox(services.GetRequiredService<ApplicationDbContext>(), services.GetRequiredService<IPayloadProtector>(), clock,
            [new Sender(channel)], services.GetRequiredService<OutboxOrderingKeyHasher>());
        var id = Guid.Empty;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            id = outbox.Enqueue(channel, "payload", options: options);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return id;
    }

    private async Task<OutboxDispatchClaim?> ClaimAsync(TimeProvider clock, string channel)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await Dispatch(scope.ServiceProvider, clock).ClaimNextAsync([channel], Ct)).Value;
    }

    private async Task CompleteAsync(TimeProvider clock, OutboxDispatchClaim claim, OutboxDeliveryReceipt receipt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True((await Dispatch(scope.ServiceProvider, clock).CompleteAsync(claim.MessageId, claim.LeaseId, receipt, Ct)).IsSuccess);
    }

    private static OutboxDispatchService Dispatch(IServiceProvider provider, TimeProvider clock) =>
        new(provider.GetRequiredService<IUnitOfWork>(), new OutboxDispatchStore(provider.GetRequiredService<ApplicationDbContext>(),
            clock, Options.Create(new OutboxOptions()), provider.GetRequiredService<IAfterCommit>(),
            provider.GetRequiredService<OutboxMetrics>()), [], clock, NullLogger<OutboxDispatchService>.Instance);

    private sealed class Sender(string key) : IChannelSender
    {
        public string Key => key;
        public Task SendAsync(string payload, CancellationToken cancellationToken) => throw new InvalidOperationException("These tests do not send.");
    }
}
