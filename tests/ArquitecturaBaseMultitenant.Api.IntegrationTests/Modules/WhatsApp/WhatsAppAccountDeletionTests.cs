using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B7 de la E11, la baja de una cuenta: en la organización, sus respuestas y notas quedan sin su nombre y la conversación que
/// tenía tomada queda sin tomar, pero el texto se queda (D4); en su espacio personal se borran sus vínculos y la
/// organización ve la conversación sin vincular. Cada paso corre en su alcance y su UoW, como en la baja real.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppAccountDeletionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deleting_accounts_forgets_authors_and_links_but_the_organization_keeps_the_chat_text()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (ownerAccount, tenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct);
        var numberId = (await WhatsAppAttentionJourney.JsonAsync(connected, Ct)).GetProperty("id").GetGuid();
        using (var bot = await owner.PutAsJsonAsync($"/api/organization/whatsapp/numbers/{numberId}/bot", Bot(), Ct))
            Assert.Equal(HttpStatusCode.OK, bot.StatusCode);

        // Una persona con su teléfono verificado escribe y vincula la conversación; la Dueña responde y deja una nota.
        using var person = WhatsAppAttentionJourney.Client(host);
        var personAccount = await AccountJourney.RegisterAsync(host, person, Ct);
        var phone = WhatsAppAttentionJourney.NewPhone();
        await VerifyPhoneAsync(personAccount.UserId, phone);
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Hola", Ct);
        await LinkAsync(host, anonymous, phoneNumberId, phone);
        var chatId = Assert.Single((await JsonAsync(owner.GetAsync("/api/organization/whatsapp/customers/conversations", Ct)))
            .GetProperty("conversations").GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        var path = $"/api/organization/whatsapp/customers/conversations/{chatId}";
        using (var reply = await WhatsAppAttentionJourney.PostAsync(owner, path + "/messages", new { text = "Te respondo" }, Ct))
            Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        using (var note = await WhatsAppAttentionJourney.PostAsync(owner, path + "/notes", new { text = "Cliente de siempre" }, Ct))
            Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        Assert.Equal(1, (await JsonAsync(person.GetAsync("/api/me/whatsapp/conversations", Ct))).GetProperty("totalCount").GetInt32());

        // Baja de la Dueña, paso de su organización.
        await DeleteStepAsync(host, ownerAccount.UserId, tenantId, TenantKind.Business);
        var conversation = await JsonAsync(owner.GetAsync(path, Ct));
        Assert.Equal(JsonValueKind.Null, conversation.GetProperty("conversation").GetProperty("assignee").ValueKind);
        var written = conversation.GetProperty("messages").GetProperty("items").EnumerateArray()
            .Where(message => message.GetProperty("text").GetString() is "Te respondo" or "Cliente de siempre").ToArray();
        Assert.Equal(2, written.Length);
        Assert.All(written, message => Assert.Equal(JsonValueKind.Null, message.GetProperty("author").ValueKind));

        // Baja de la persona, paso de su espacio personal: el vínculo y sus copias se van; el chat de la organización, no.
        await DeleteStepAsync(host, personAccount.UserId, await PersonalSpaceAsync(host, personAccount.UserId), TenantKind.Personal);
        Assert.Equal(0, (await JsonAsync(person.GetAsync("/api/me/whatsapp/conversations", Ct))).GetProperty("totalCount").GetInt32());
        var after = await JsonAsync(owner.GetAsync(path, Ct));
        Assert.False(after.GetProperty("conversation").GetProperty("linkedToAccount").GetBoolean());
        Assert.Contains(after.GetProperty("messages").GetProperty("items").EnumerateArray(), message => message.GetProperty("text").GetString() == "Hola");
    }

    private static async Task DeleteStepAsync(WebApplicationFactory<Program> host, Guid userId, Guid tenantId, TenantKind kind)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, kind);
        var participant = services.GetServices<IAccountDeletionParticipant>().Single(value => value.GetType().Name == "WhatsAppAccountDeletionParticipant");
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var done = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            await participant.ExecuteAsync(new AccountDeletionContext(userId, nowUtc, tenantId, kind), ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(done.IsSuccess);
    }

    private static async Task<Guid> PersonalSpaceAsync(WebApplicationFactory<Program> host, Guid userId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct))
            .Single(access => access.Kind == TenantKind.Personal).TenantId;
    }

    private async Task VerifyPhoneAsync(Guid userId, string phone)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var method = LoginMethod.CreatePhone(userId, PhoneNumber.Create(phone).Value);
            method.Verify(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            scope.ServiceProvider.GetRequiredService<ILoginMethodRepository>().Add(method);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task LinkAsync(WebApplicationFactory<Program> host, HttpClient client, string phoneNumberId, string from)
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
                    interactive = new { type = "button_reply", button_reply = new { id = BotButtons.LinkAccount, title = "Vincular mi cuenta" } },
                    timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                } },
            } } } } },
        });
        using var received = await WhatsAppAttentionJourney.WebhookAsync(client, body, Ct);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await host.Services.GetServices<IHostedService>().OfType<Infrastructure.Modules.WhatsApp.Inbound.WhatsAppInboundProcessor>().Single()
            .ProcessPendingAsync(Ct);
    }

    private static object Bot() => new
    {
        welcome = "Bienvenidos", outOfHours = "Estamos cerrados",
        schedule = new
        {
            timeZoneId = "America/Argentina/Buenos_Aires",
            days = Enum.GetValues<DayOfWeek>().Select(day => new { day = day.ToString(), from = "00:00:00", to = "23:59:59" }).ToArray(),
        },
        menu = Array.Empty<object>(), askPromotions = false, offerAccountLink = true, employeesTeamId = (Guid?)null, version = 0u,
    };

    private static async Task<JsonElement> JsonAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }
}
