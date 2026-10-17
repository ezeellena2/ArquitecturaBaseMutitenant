using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B3 de la E11: el bot de cada número. Sin configurar responde con los valores iniciales; el menú pasa a equipos de
/// Clientes y los empleados a uno de Internos; el cliente recibe la lista, elige y le preguntan una vez por promociones;
/// fuera de horario recibe ese texto y fuera de las 24 horas no recibe nada. El bot de otra organización no se ve.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppAttentionBotTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_bot_starts_with_defaults_keeps_teams_of_each_inbox_and_is_not_visible_to_another_organization()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var numberId = await ConnectAsync(client);
        var path = $"/api/organization/whatsapp/numbers/{numberId}/bot";

        var initial = await WhatsAppAttentionJourney.JsonAsync(await client.GetAsync(path, Ct), Ct);
        Assert.Equal(0u, initial.GetProperty("version").GetUInt32());
        Assert.Equal("¡Hola! Gracias por escribirnos. En breve te respondemos.", initial.GetProperty("welcome").GetString());
        Assert.Equal("America/Argentina/Buenos_Aires", initial.GetProperty("schedule").GetProperty("timeZoneId").GetString());
        Assert.Equal(5, initial.GetProperty("schedule").GetProperty("days").GetArrayLength());
        Assert.Equal(0, initial.GetProperty("menu").GetArrayLength());

        var (sales, staff) = await TeamsAsync(host, tenant);
        using var wrongInbox = await client.PutAsJsonAsync(path, Bot([Team("Equipo", staff)], 0, staff), Ct);
        Assert.Equal((HttpStatusCode.NotFound, WhatsAppErrors.TeamNotFoundCode), (wrongInbox.StatusCode, await CodeAsync(wrongInbox)));
        using var tooMany = await client.PutAsJsonAsync(path, Bot([.. Enumerable.Range(1, 11).Select(index => Reply("Opción " + index))], 0), Ct);
        Assert.Equal((HttpStatusCode.BadRequest, WhatsAppErrors.BotTooManyOptionsCode), (tooMany.StatusCode, await CodeAsync(tooMany)));

        using var saved = await client.PutAsJsonAsync(path, Bot([Team("Ventas", sales), Reply("Horarios")], 0, staff), Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var bot = await WhatsAppAttentionJourney.JsonAsync(saved, Ct);
        Assert.NotEqual(0u, bot.GetProperty("version").GetUInt32());
        Assert.Equal(staff, bot.GetProperty("employeesTeamId").GetGuid());
        Assert.Equal(["Ventas", "Horarios"], bot.GetProperty("menu").EnumerateArray().Select(option => option.GetProperty("label").GetString()));
        using var stale = await client.PutAsJsonAsync(path, Bot([], 0), Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var other = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, other, Ct);
        using var foreign = await other.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var foreignUpdate = await other.PutAsJsonAsync(path, Bot([], bot.GetProperty("version").GetUInt32()), Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
    }

    [Fact]
    public async Task A_customer_gets_the_menu_picks_a_team_answers_promotions_once_and_an_employee_goes_to_its_team()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var numberId = await ConnectAsync(client);
        var phoneNumberId = await PhoneNumberIdAsync(host, numberId);
        var (sales, staff) = await TeamsAsync(host, tenant);
        using var saved = await client.PutAsJsonAsync($"/api/organization/whatsapp/numbers/{numberId}/bot",
            Bot([Team("Ventas", sales), Reply("Horarios")], 0, staff, askPromotions: true), Ct);
        var salesOption = (await WhatsAppAttentionJourney.JsonAsync(saved, Ct)).GetProperty("menu")[0].GetProperty("id").GetGuid();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var customer = NewPhone();

        await InboundAsync(host, anonymous, phoneNumberId, customer, Text("Hola"));
        var menu = Assert.Single(await OutboxAsync(host, customer));
        Assert.Equal("Bienvenidos", menu.Body);
        Assert.Equal("Ver opciones", menu.ListButton);
        Assert.Equal([BotButtons.Option(salesOption), menu.ListRows![1].Id], menu.ListRows.Select(row => row.Id));
        Assert.Equal(["Ventas", "Horarios"], menu.ListRows.Select(row => row.Title));

        await InboundAsync(host, anonymous, phoneNumberId, customer, Choice("list_reply", BotButtons.Option(salesOption), "Ventas"));
        await InboundAsync(host, anonymous, phoneNumberId, customer, Choice("button_reply", BotButtons.PromotionsYes, "Sí"));
        await InboundAsync(host, anonymous, phoneNumberId, customer, Text("Gracias"));
        var (chat, contact, messages) = await CustomerChatAsync(host, tenant, customer);
        Assert.Equal((sales, (Guid?)null), (chat.TeamId, chat.AssigneeUserId));
        Assert.Equal((WhatsAppConsent.Accepted, WhatsAppConsentSource.Bot), (contact.Consent, contact.ConsentSource));
        Assert.NotNull(contact.PromotionsAskedAtUtc);
        // La entrada lleva la hora de Meta (al segundo) y la salida, la del bot: cada sentido se compara en su orden.
        Assert.Equal(["Hola", "Ventas", "Sí", "Gracias"], Texts(messages, WhatsAppMessageDirection.Inbound));
        Assert.Equal(["Bienvenidos", "¿Querés recibir promociones por WhatsApp? Podés darte de baja cuando quieras.",
            "Listo, te vamos a mandar promociones."], Texts(messages, WhatsAppMessageDirection.Outbound));
        Assert.All(messages.Where(message => message.Direction == WhatsAppMessageDirection.Outbound), message => Assert.Null(message.AuthorUserId));
        Assert.Equal(3, (await OutboxAsync(host, customer)).Count);

        var employeePhone = NewPhone();
        await OrganizationJourney.AddActiveMemberAsync(factory, tenant, await VerifiedPhoneAccountAsync(employeePhone), Ct);
        await InboundAsync(host, anonymous, phoneNumberId, employeePhone, Text("Soy del equipo"));
        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
        var internalChat = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChat>().AsNoTracking()
            .SingleAsync(value => value.WaId == employeePhone, Ct);
        Assert.Equal((WhatsAppInbox.Internal, (Guid?)staff), (internalChat.Inbox, internalChat.TeamId));
        Assert.Empty(await OutboxAsync(host, employeePhone));
    }

    [Fact]
    public async Task Out_of_hours_the_customer_gets_that_text_and_after_24_hours_the_bot_stays_silent()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var numberId = await ConnectAsync(client);
        var phoneNumberId = await PhoneNumberIdAsync(host, numberId);
        using var saved = await client.PutAsJsonAsync($"/api/organization/whatsapp/numbers/{numberId}/bot", Bot([], 0, days: []), Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var anonymous = WhatsAppAttentionJourney.Client(host);

        var customer = NewPhone();
        await InboundAsync(host, anonymous, phoneNumberId, customer, Text("Hola"));
        Assert.Equal("Estamos cerrados", Assert.Single(await OutboxAsync(host, customer)).Body);

        var late = NewPhone();
        await InboundAsync(host, anonymous, phoneNumberId, late, Text("Hola"), TimeProvider.System.GetUtcNow().AddHours(-25));
        Assert.Empty(await OutboxAsync(host, late));
    }

    private static IEnumerable<string?> Texts(IEnumerable<WhatsAppChatMessage> messages, WhatsAppMessageDirection direction) =>
        messages.Where(message => message.Direction == direction).Select(message => message.Text);

    private static object Bot(object[] menu, uint version, Guid? employeesTeamId = null, bool askPromotions = false, object[]? days = null) => new
    {
        welcome = "Bienvenidos", outOfHours = "Estamos cerrados",
        schedule = new
        {
            timeZoneId = "America/Argentina/Buenos_Aires",
            days = days ?? [.. Enum.GetValues<DayOfWeek>().Select(day => (object)new { day = day.ToString(), from = "00:00:00", to = "23:59:59" })],
        },
        menu, askPromotions, offerAccountLink = false, employeesTeamId, version,
    };

    private static object Team(string label, Guid teamId) => new { label, action = "Team", teamId };
    private static object Reply(string label) => new { label, action = "Reply", reply = "Abrimos de 9 a 18" };

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await WhatsAppAttentionJourney.JsonAsync(response, Ct)).GetProperty("code").GetString();

    private static async Task<Guid> ConnectAsync(HttpClient client)
    {
        using var connected = await WhatsAppAttentionJourney.ConnectAsync(client, WhatsAppAttentionJourney.NewNumberId(), Ct);
        Assert.Equal(HttpStatusCode.Created, connected.StatusCode);
        return (await WhatsAppAttentionJourney.JsonAsync(connected, Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<string> PhoneNumberIdAsync(WebApplicationFactory<Program> host, Guid numberId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChannel>().AsNoTracking()
            .Where(value => value.Id == numberId).Select(value => value.PhoneNumberId).SingleAsync(Ct);
    }

    /// <summary>Un equipo de Clientes y uno de Internos; los equipos se administran en B4.</summary>
    private static async Task<(Guid Sales, Guid Staff)> TeamsAsync(WebApplicationFactory<Program> host, Guid tenantId)
    {
        var sales = WhatsAppTeam.Create("Ventas", WhatsAppInbox.Customers, null, []);
        var staff = WhatsAppTeam.Create("Equipo", WhatsAppInbox.Internal, null, []);
        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<WhatsAppTeam>().AddRange(sales, staff);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return (sales.Id, staff.Id);
    }

    private static async Task InboundAsync(WebApplicationFactory<Program> host, HttpClient client, string phoneNumberId, string from, object content,
        DateTimeOffset? at = null)
    {
        var body = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-" + phoneNumberId, changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
                messages = new[] { Merge(new Dictionary<string, object>
                {
                    ["from"] = from.TrimStart('+'), ["id"] = "wamid.bot." + Guid.NewGuid().ToString("N"),
                    ["timestamp"] = (at ?? TimeProvider.System.GetUtcNow()).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                }, content) },
            } } } } },
        });
        using var received = await WhatsAppAttentionJourney.WebhookAsync(client, body, Ct);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await host.Services.GetServices<IHostedService>().OfType<WhatsAppInboundProcessor>().Single().ProcessPendingAsync(Ct);
    }

    private static Dictionary<string, object> Merge(Dictionary<string, object> message, object content)
    {
        foreach (var property in JsonSerializer.SerializeToElement(content).EnumerateObject()) message[property.Name] = property.Value;
        return message;
    }

    private static object Text(string body) => new { type = "text", text = new { body } };

    private static object Choice(string kind, string id, string title) => kind == "list_reply"
        ? new { type = "interactive", interactive = (object)new { type = kind, list_reply = new { id, title } } }
        : new { type = "interactive", interactive = (object)new { type = kind, button_reply = new { id, title } } };

    /// <summary>Lo que el bot dejó en la cola para ese número, descifrado (solo en la prueba).</summary>
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

    private static async Task<(WhatsAppChat Chat, WhatsAppCustomer Contact, IReadOnlyList<WhatsAppChatMessage> Messages)> CustomerChatAsync(
        WebApplicationFactory<Program> host, Guid tenantId, string waId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var chat = await db.Set<WhatsAppChat>().AsNoTracking().SingleAsync(value => value.WaId == waId, Ct);
        var contact = await db.Set<WhatsAppCustomer>().AsNoTracking().SingleAsync(value => value.WaId == waId, Ct);
        var messages = await db.Set<WhatsAppChatMessage>().AsNoTracking().Where(value => value.ChatId == chat.Id)
            .OrderBy(value => value.CreatedAtUtc).ThenBy(value => value.Id).ToArrayAsync(Ct);
        return (chat, contact, messages);
    }

    private async Task<Guid> VerifiedPhoneAccountAsync(string phone)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = Guid.Empty;
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            user = (await scope.ServiceProvider.GetRequiredService<IUserRepository>().CreateAsync("Empleada", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = LoginMethod.CreatePhone(user, PhoneNumber.Create(phone).Value);
            method.Verify(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            scope.ServiceProvider.GetRequiredService<ILoginMethodRepository>().Add(method);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return user;
    }

    private static string NewPhone() => "+54911" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(30_000_000, 59_999_999)
        .ToString(System.Globalization.CultureInfo.InvariantCulture);
}
