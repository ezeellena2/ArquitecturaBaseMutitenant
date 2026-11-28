using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B5 de la E11, vínculo con la cuenta (D2, D5): el bot lo ofrece solo a un teléfono verificado de una cuenta; la persona
/// ve en «Mensajes» solo lo que pasó desde que vinculó; si el número pasa a otra cuenta, no se le copia nada más; puede
/// desvincular; nadie más ve su vínculo.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppConversationLinkTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_verified_customer_links_reads_only_what_came_after_and_a_recycled_number_never_adds_to_it()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct);
        var numberId = (await WhatsAppAttentionJourney.JsonAsync(connected, Ct)).GetProperty("id").GetGuid();
        using (var bot = await owner.PutAsJsonAsync($"/api/organization/whatsapp/numbers/{numberId}/bot", Bot(), Ct))
            Assert.Equal(HttpStatusCode.OK, bot.StatusCode);

        using var person = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, person, Ct);
        var phone = WhatsAppAttentionJourney.NewPhone();
        await VerifyPhoneAsync(account.UserId, phone);
        using var anonymous = WhatsAppAttentionJourney.Client(host);

        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Hola antes de vincular", Ct);
        var sent = await OutboxAsync(host, phone);
        var offer = sent[^1];
        Assert.Equal([BotButtons.LinkAccount], offer.ReplyButtons!.Select(button => button.Id));
        await ChoiceAsync(host, anonymous, phoneNumberId, phone, BotButtons.LinkAccount);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Escribo después de vincular", Ct);

        var rows = await JsonAsync(await owner.GetAsync("/api/organization/whatsapp/customers/conversations", Ct));
        var row = Assert.Single(rows.GetProperty("conversations").GetProperty("items").EnumerateArray());
        Assert.True(row.GetProperty("linkedToAccount").GetBoolean());
        using (var answer = await WhatsAppAttentionJourney.PostAsync(owner,
            $"/api/organization/whatsapp/customers/conversations/{row.GetProperty("id").GetGuid()}/messages", new { text = "Te respondo" }, Ct))
            Assert.Equal(HttpStatusCode.Created, answer.StatusCode);
        Assert.True(Single(await JsonAsync(await owner.GetAsync(
            $"/api/organization/whatsapp/contacts?search={Uri.EscapeDataString(phone)}", Ct)), "contacts").GetProperty("linkedToAccount").GetBoolean());

        var mine = Single(await JsonAsync(await person.GetAsync("/api/me/whatsapp/conversations", Ct)), null);
        var linkId = mine.GetProperty("id").GetGuid();
        var texts = await TextsAsync(person, linkId);
        Assert.Contains("Escribo después de vincular", texts);
        Assert.Contains("Te respondo", texts);
        Assert.DoesNotContain("Hola antes de vincular", texts);

        // El número pasa a otra cuenta sin quitar el vínculo previo (vínculo huérfano): ni lo entrante ni lo saliente se copian.
        using var next = WhatsAppAttentionJourney.Client(host);
        var nextAccount = await AccountJourney.RegisterAsync(host, next, Ct);
        await MovePhoneAsync(account.UserId, nextAccount.UserId, phone);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Soy la nueva dueña del número", Ct);
        using (var replyToNext = await WhatsAppAttentionJourney.PostAsync(owner,
            $"/api/organization/whatsapp/customers/conversations/{row.GetProperty("id").GetGuid()}/messages", new { text = "Respuesta para la nueva dueña" }, Ct))
            Assert.Equal(HttpStatusCode.Created, replyToNext.StatusCode);
        var personTexts = await TextsAsync(person, linkId);
        Assert.DoesNotContain("Soy la nueva dueña del número", personTexts);
        Assert.DoesNotContain("Respuesta para la nueva dueña", personTexts);
        Assert.Equal(0, (await JsonAsync(await next.GetAsync("/api/me/whatsapp/conversations", Ct))).GetProperty("totalCount").GetInt32());
        using (var foreign = await next.GetAsync($"/api/me/whatsapp/conversations/{linkId}", Ct))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using (var foreignUnlink = await next.DeleteAsync($"/api/me/whatsapp/conversations/{linkId}/link", Ct))
            Assert.Equal(HttpStatusCode.NotFound, foreignUnlink.StatusCode);

        using (var unlinked = await person.DeleteAsync($"/api/me/whatsapp/conversations/{linkId}/link", Ct))
            Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);
        Assert.Equal(0, (await JsonAsync(await person.GetAsync("/api/me/whatsapp/conversations", Ct))).GetProperty("totalCount").GetInt32());
        var after = await JsonAsync(await owner.GetAsync("/api/organization/whatsapp/customers/conversations", Ct));
        Assert.False(Assert.Single(after.GetProperty("conversations").GetProperty("items").EnumerateArray()).GetProperty("linkedToAccount").GetBoolean());
    }

    [Fact]
    public async Task Removing_a_phone_deletes_its_conversation_link_and_stops_copying_replies_to_the_previous_owner()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, tenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct);
        var numberId = (await WhatsAppAttentionJourney.JsonAsync(connected, Ct)).GetProperty("id").GetGuid();
        using (var bot = await owner.PutAsJsonAsync($"/api/organization/whatsapp/numbers/{numberId}/bot", Bot(), Ct))
            Assert.Equal(HttpStatusCode.OK, bot.StatusCode);

        // A vincula su chat con su teléfono verificado.
        using var personA = WhatsAppAttentionJourney.Client(host);
        var accountA = await AccountJourney.RegisterAsync(host, personA, Ct);
        var phone = WhatsAppAttentionJourney.NewPhone();
        var methodA = await VerifyPhoneAsync(accountA.UserId, phone);
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Hola soy A", Ct);
        await ChoiceAsync(host, anonymous, phoneNumberId, phone, BotButtons.LinkAccount);

        var chatId = Assert.Single((await JsonAsync(await owner.GetAsync("/api/organization/whatsapp/customers/conversations", Ct)))
            .GetProperty("conversations").GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        var linkId = Single(await JsonAsync(await personA.GetAsync("/api/me/whatsapp/conversations", Ct)), null)
            .GetProperty("id").GetGuid();

        // A quita el teléfono: su vínculo se borra de la base y la conversación deja de figurar vinculada.
        using (var removed = await personA.DeleteAsync($"/api/me/login-methods/{methodA}", Ct))
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(0, (await JsonAsync(await personA.GetAsync("/api/me/whatsapp/conversations", Ct))).GetProperty("totalCount").GetInt32());
        using (var gone = await personA.GetAsync($"/api/me/whatsapp/conversations/{linkId}", Ct))
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        var orgRowAfterRemoval = Assert.Single((await JsonAsync(await owner.GetAsync("/api/organization/whatsapp/customers/conversations", Ct)))
            .GetProperty("conversations").GetProperty("items").EnumerateArray());
        Assert.False(orgRowAfterRemoval.GetProperty("linkedToAccount").GetBoolean());
        await using (var scope = host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
            Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Set<WhatsAppConversationLink>().AnyAsync(value => value.Id == linkId, Ct));
        }

        // B verifica el mismo número y la organización le contesta: nada aparece en «Mensajes» de A.
        using var personB = WhatsAppAttentionJourney.Client(host);
        var accountB = await AccountJourney.RegisterAsync(host, personB, Ct);
        await VerifyPhoneAsync(accountB.UserId, phone);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Hola soy B", Ct);
        using (var replyToB = await WhatsAppAttentionJourney.PostAsync(owner,
            $"/api/organization/whatsapp/customers/conversations/{chatId}/messages", new { text = "Respuesta privada para B" }, Ct))
            Assert.Equal(HttpStatusCode.Created, replyToB.StatusCode);

        Assert.Equal(0, (await JsonAsync(await personA.GetAsync("/api/me/whatsapp/conversations", Ct))).GetProperty("totalCount").GetInt32());
        using (var stillGone = await personA.GetAsync($"/api/me/whatsapp/conversations/{linkId}", Ct))
            Assert.Equal(HttpStatusCode.NotFound, stillGone.StatusCode);
    }

    private static object Bot() => new
    {
        welcome = "Bienvenidos",
        outOfHours = "Estamos cerrados",
        schedule = new
        {
            timeZoneId = "America/Argentina/Buenos_Aires",
            days = Enum.GetValues<DayOfWeek>().Select(day => new { day = day.ToString(), from = "00:00:00", to = "23:59:59" }).ToArray(),
        },
        menu = Array.Empty<object>(),
        askPromotions = false,
        offerAccountLink = true,
        employeesTeamId = (Guid?)null,
        version = 0u,
    };

    private static async Task<IReadOnlyList<string?>> TextsAsync(HttpClient person, Guid linkId) =>
        [.. (await JsonAsync(await person.GetAsync($"/api/me/whatsapp/conversations/{linkId}", Ct))).GetProperty("messages").GetProperty("items")
            .EnumerateArray().Select(message => message.GetProperty("text").GetString())];

    private async Task<Guid> VerifyPhoneAsync(Guid userId, string phone)
    {
        var methodId = Guid.Empty;
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var method = LoginMethod.CreatePhone(userId, PhoneNumber.Create(phone).Value);
            method.Verify(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            scope.ServiceProvider.GetRequiredService<ILoginMethodRepository>().Add(method);
            methodId = method.Id;
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return methodId;
    }

    private async Task MovePhoneAsync(Guid from, Guid to, string phone)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LoginMethods.Remove(await db.LoginMethods.SingleAsync(method => method.UserId == from && method.Value == phone, ct));
            await db.SaveChangesAsync(ct);
            var method = LoginMethod.CreatePhone(to, PhoneNumber.Create(phone).Value);
            method.Verify(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            scope.ServiceProvider.GetRequiredService<ILoginMethodRepository>().Add(method);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task ChoiceAsync(WebApplicationFactory<Program> host, HttpClient client, string phoneNumberId, string from, string id)
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
                    from = from.TrimStart('+'), id = "wamid.link." + Guid.NewGuid().ToString("N"), type = "interactive",
                    interactive = new { type = "button_reply", button_reply = new { id, title = "Vincular mi cuenta" } },
                    timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                } },
            } } } } },
        });
        using var received = await WhatsAppAttentionJourney.WebhookAsync(client, body, Ct);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await host.Services.GetServices<IHostedService>().OfType<Infrastructure.Modules.WhatsApp.Inbound.WhatsAppInboundProcessor>().Single()
            .ProcessPendingAsync(Ct);
    }

    private static async Task<IReadOnlyList<WhatsAppOutboundMessage>> OutboxAsync(WebApplicationFactory<Program> host, string to)
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
        return [.. messages.Where(message => message.To == to)];
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using (response) return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private static JsonElement Single(JsonElement page, string? property) =>
        Assert.Single((property is null ? page : page.GetProperty(property)).GetProperty("items").EnumerateArray());
}
