using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B3 de la E11, bot de Plataforma: «Entrar a mi cuenta» va primero, ocupa un lugar de los 10 y se conserva aunque el
/// pedido no la mande. Con las consultas prendidas la entrada queda en su conversación, el bot manda el menú y esa opción
/// responde con el ingreso de la Etapa 8, sin el pie de «solo sirve para entrar» y sin dejar el enlace en la conversación.
/// Base propia: el número de Plataforma es global.
/// </summary>
public sealed class PlatformWhatsAppBotTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_system_option_goes_first_and_with_consultations_on_it_answers_with_the_sign_in_reply()
    {
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), settings: new Dictionary<string, string>
        {
            ["WhatsApp:PhoneNumberId"] = phoneNumberId, ["WhatsApp:AccessToken"] = "configured-token",
        });
        using var client = await OperatorAsync(host);

        var initial = await WhatsAppAttentionJourney.JsonAsync(await client.GetAsync("/api/platform/whatsapp/bot", Ct), Ct);
        var system = Assert.Single(initial.GetProperty("menu").EnumerateArray());
        Assert.True(system.GetProperty("isSystem").GetBoolean());
        Assert.Equal(PlatformWhatsAppBot.SignInOptionId, system.GetProperty("id").GetGuid());
        Assert.Equal(0u, initial.GetProperty("version").GetUInt32());

        using var tooMany = await client.PutAsJsonAsync("/api/platform/whatsapp/bot",
            Bot([.. Enumerable.Range(1, 10).Select(index => (object)new { label = "Opción " + index, action = "Reply", reply = "Respuesta" })], 0), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(WhatsAppErrors.BotTooManyOptionsCode, (await WhatsAppAttentionJourney.JsonAsync(tooMany, Ct)).GetProperty("code").GetString());
        using var saved = await client.PutAsJsonAsync("/api/platform/whatsapp/bot",
            Bot([new { id = PlatformWhatsAppBot.SignInOptionId, label = "Otra", action = "Reply", reply = "No se guarda" },
                new { label = "Horarios", action = "Reply", reply = "Abrimos de 9 a 18" }], 0), Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var menu = (await WhatsAppAttentionJourney.JsonAsync(saved, Ct)).GetProperty("menu").EnumerateArray().ToArray();
        Assert.Equal([(PlatformWhatsAppBot.SignInOptionId, true), (menu[1].GetProperty("id").GetGuid(), false)],
            menu.Select(option => (option.GetProperty("id").GetGuid(), option.GetProperty("isSystem").GetBoolean())));
        Assert.Equal("Horarios", menu[1].GetProperty("label").GetString());

        var number = (await WhatsAppAttentionJourney.JsonAsync(await client.GetAsync("/api/platform/whatsapp/number", Ct), Ct)).GetProperty("number");
        using var consultations = await client.PutAsJsonAsync("/api/platform/whatsapp/number",
            new { consultationsEnabled = true, version = number.GetProperty("version").GetUInt32() }, Ct);
        Assert.Equal(HttpStatusCode.OK, consultations.StatusCode);

        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var person = "+54911" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(30_000_000, 59_999_999)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        await InboundAsync(host, anonymous, phoneNumberId, person, new { type = "text", text = new { body = "Hola" } });
        var welcome = Assert.Single(await OutboxAsync(host, person));
        Assert.Equal("Bienvenidos", welcome.Body);
        Assert.Equal([BotButtons.Option(PlatformWhatsAppBot.SignInOptionId), BotButtons.Option(menu[1].GetProperty("id").GetGuid())],
            welcome.ListRows!.Select(row => row.Id));
        Assert.Equal("Entrar a mi cuenta", welcome.ListRows![0].Title);

        await InboundAsync(host, anonymous, phoneNumberId, person, new { type = "interactive", interactive = new { type = "list_reply",
            list_reply = new { id = BotButtons.Option(PlatformWhatsAppBot.SignInOptionId), title = "Entrar a mi cuenta" } } });
        var replies = await OutboxAsync(host, person);
        Assert.Equal(2, replies.Count);
        Assert.NotEqual("Bienvenidos", replies[1].Body);
        Assert.Null(replies[1].Footer);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var chat = await db.Set<PlatformWhatsAppChat>().AsNoTracking().SingleAsync(value => value.WaId == person, Ct);
        var messages = await db.Set<PlatformWhatsAppChatMessage>().AsNoTracking().Where(value => value.ChatId == chat.Id)
            .OrderBy(value => value.CreatedAtUtc).ThenBy(value => value.Id).Select(value => new { value.Direction, value.Text }).ToArrayAsync(Ct);
        Assert.Equal(["Hola", "Entrar a mi cuenta"], messages.Where(message => message.Direction == WhatsAppMessageDirection.Inbound).Select(message => message.Text));
        Assert.Equal(["Bienvenidos"], messages.Where(message => message.Direction == WhatsAppMessageDirection.Outbound).Select(message => message.Text));
    }

    [Fact]
    public async Task Operators_answer_consultations_and_only_those_who_read_accounts_see_the_account()
    {
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), settings: new Dictionary<string, string>
        {
            ["WhatsApp:PhoneNumberId"] = phoneNumberId, ["WhatsApp:AccessToken"] = "configured-token",
        });
        using var manager = await OperatorAsync(host, PlatformPermissions.WhatsApp.Manage, PlatformPermissions.WhatsApp.Attend, PlatformPermissions.Accounts.Read);
        using var attendant = await OperatorAsync(host, PlatformPermissions.WhatsApp.Attend);
        var number = (await WhatsAppAttentionJourney.JsonAsync(await manager.GetAsync("/api/platform/whatsapp/number", Ct), Ct)).GetProperty("number");
        Assert.False((await WhatsAppAttentionJourney.JsonAsync(await manager.GetAsync("/api/platform/whatsapp/number", Ct), Ct))
            .GetProperty("consultationsEnabled").GetBoolean());
        using (var on = await manager.PutAsJsonAsync("/api/platform/whatsapp/number",
            new { consultationsEnabled = true, version = number.GetProperty("version").GetUInt32() }, Ct))
            Assert.Equal(HttpStatusCode.OK, on.StatusCode);

        var person = WhatsAppAttentionJourney.NewPhone();
        var account = await VerifiedPhoneAccountAsync(host, person);
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, person, "Necesito ayuda", Ct);

        var seen = Consultation(await WhatsAppAttentionJourney.JsonAsync(await manager.GetAsync("/api/platform/whatsapp/conversations", Ct), Ct), person);
        Assert.Equal(account, seen.GetProperty("accountId").GetGuid());
        var list = await WhatsAppAttentionJourney.JsonAsync(await attendant.GetAsync("/api/platform/whatsapp/conversations", Ct), Ct);
        var row = Consultation(list, person);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("accountId").ValueKind);
        Assert.Equal(0, list.GetProperty("counts").GetProperty("internal").GetInt32());
        var id = row.GetProperty("id").GetGuid();

        using (var taken = await attendant.PostAsync($"/api/platform/whatsapp/conversations/{id}/take", null, Ct))
            Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        using (var blocked = await WhatsAppAttentionJourney.PostAsync(manager, $"/api/platform/whatsapp/conversations/{id}/messages", new { text = "Hola" }, Ct))
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using (var sent = await WhatsAppAttentionJourney.PostAsync(attendant, $"/api/platform/whatsapp/conversations/{id}/messages", new { text = "Te ayudo" }, Ct))
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        Assert.Contains(await OutboxAsync(host, person), message => message.Body == "Te ayudo");
    }

    private static JsonElement Consultation(JsonElement list, string waId) =>
        list.GetProperty("conversations").GetProperty("items").EnumerateArray().Single(row => row.GetProperty("displayNumber").GetString() == waId);

    private static async Task<Guid> VerifiedPhoneAccountAsync(WebApplicationFactory<Program> host, string phone)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var user = Guid.Empty;
        await scope.ServiceProvider.GetRequiredService<Application.Interfaces.Persistence.IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            user = (await scope.ServiceProvider.GetRequiredService<Application.Interfaces.Persistence.IUserRepository>()
                .CreateAsync("Persona", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = Domain.Authentication.LoginMethod.CreatePhone(user, Domain.ValueObjects.PhoneNumber.Create(phone).Value);
            method.Verify(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            scope.ServiceProvider.GetRequiredService<Application.Interfaces.Persistence.ILoginMethodRepository>().Add(method);
            return Domain.Results.Result.Success();
        }, Application.Interfaces.Persistence.CommitPolicy.OnSuccess, Ct);
        return user;
    }

    private static object Bot(object[] menu, uint version) => new
    {
        welcome = "Bienvenidos", outOfHours = "Estamos cerrados",
        schedule = new
        {
            timeZoneId = "America/Argentina/Buenos_Aires",
            days = Enum.GetValues<DayOfWeek>().Select(day => new { day = day.ToString(), from = "00:00:00", to = "23:59:59" }).ToArray(),
        },
        menu, version,
    };

    private static async Task InboundAsync(WebApplicationFactory<Program> host, HttpClient client, string phoneNumberId, string from, object content)
    {
        var message = new Dictionary<string, object>
        {
            ["from"] = from.TrimStart('+'), ["id"] = "wamid.platform." + Guid.NewGuid().ToString("N"),
            ["timestamp"] = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        foreach (var property in JsonSerializer.SerializeToElement(content).EnumerateObject()) message[property.Name] = property.Value;
        var body = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-platform", changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
                messages = new[] { message },
            } } } } },
        });
        using var received = await WhatsAppAttentionJourney.WebhookAsync(client, body, Ct);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await host.Services.GetServices<IHostedService>().OfType<WhatsAppInboundProcessor>().Single().ProcessPendingAsync(Ct);
    }

    private static async Task<IReadOnlyList<WhatsAppOutboundMessage>> OutboxAsync(WebApplicationFactory<Program> host, string to)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var protector = scope.ServiceProvider.GetRequiredService<IPayloadProtector>();
        var rows = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(value => value.Channel == "whatsapp").OrderBy(value => value.CreatedAtUtc).ThenBy(value => value.Id).ToArrayAsync(Ct);
        return [.. rows.Select(row => Read(protector, row.EncryptedPayload)).OfType<WhatsAppOutboundMessage>().Where(message => message.To == to)];
    }

    /// <summary>La base es compartida y cada host tiene sus claves: lo que cifró otro host no es de esta prueba.</summary>
    private static WhatsAppOutboundMessage? Read(IPayloadProtector protector, string payload)
    {
        try { return JsonSerializer.Deserialize<WhatsAppOutboundMessage>(protector.Unprotect(payload)); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    private static async Task<HttpClient> OperatorAsync(WebApplicationFactory<Program> host, params string[] permissions)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, permissions.Length == 0 ? [PlatformPermissions.WhatsApp.Manage] : permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }
}
