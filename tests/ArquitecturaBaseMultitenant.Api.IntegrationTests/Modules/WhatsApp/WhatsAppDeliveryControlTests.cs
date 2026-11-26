using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// La orden privada <c>delivery</c> del recorrido para un mensaje del chat (B-2 de la puerta de la base 2): deja «Sin
/// confirmar» el último mensaje que salió de la conversación de un teléfono de prueba, en la organización o en Plataforma, y
/// le borra la revisión para que se revise otra vez. En Plataforma, sin una consulta propia, arma la mínima sin prender las
/// consultas. Las rutas reales la leen y la revisan.
/// </summary>
public sealed class WhatsAppDeliveryControlTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Customers = "/api/organization/whatsapp/customers";
    private const string Platform = "/api/platform/whatsapp";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_last_reply_of_an_organization_conversation_is_left_unconfirmed_and_can_be_reviewed_again()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct)).Dispose();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        const string customer = "+5491134567871";
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, "Hola", Ct);
        var chat = (await JsonAsync(owner.GetAsync($"{Customers}/conversations", Ct))).GetProperty("conversations").GetProperty("items")
            .EnumerateArray().Single(row => row.GetProperty("displayNumber").GetString() == customer).GetProperty("id").GetGuid();
        var reply = await SendAsync(owner, $"{Customers}/conversations/{chat}/messages", "Te ayudo");
        await SendAsync(owner, $"{Customers}/conversations/{chat}/notes", "Nota que no sale");

        var order = new { kind = "chatMessage", scope = "organization", phone = customer, outcome = "Uncertain", tenantId = tenant.ToString() };
        var result = await OrderAsync(host, order, tenant);
        Assert.Equal((chat, reply), (result.GetProperty("chatId").GetGuid(), result.GetProperty("messageId").GetGuid()));
        Assert.Equal("Uncertain", Message(await JsonAsync(owner.GetAsync($"{Customers}/conversations/{chat}", Ct)), reply).GetProperty("status").GetString());
        var reviewed = await JsonAsync(owner.PostAsync($"{Customers}/conversations/{chat}/messages/{reply}/review", null, Ct));
        Assert.Equal(JsonValueKind.String, reviewed.GetProperty("reviewedAtUtc").ValueKind);

        // Otra vez: la revisión se borra y se puede revisar de nuevo, sobre el mismo mensaje.
        result = await OrderAsync(host, order, tenant);
        Assert.Equal(reply, result.GetProperty("messageId").GetGuid());
        var again = Message(await JsonAsync(owner.GetAsync($"{Customers}/conversations/{chat}", Ct)), reply);
        Assert.Equal(("Uncertain", JsonValueKind.Null), (again.GetProperty("status").GetString(), again.GetProperty("reviewedAtUtc").ValueKind));

        // Un teléfono que no es de la fixture, uno sin conversación o Plataforma con organización no se aceptan.
        foreach (var bad in new object[]
                 {
                     new { kind = "chatMessage", scope = "organization", phone = "+5491155550000", outcome = "Uncertain" },
                     new { kind = "chatMessage", scope = "organization", phone = "+5491134567879", outcome = "Uncertain" },
                     new { kind = "chatMessage", scope = "organization", phone = customer, outcome = "Failed" },
                     new { kind = "chatMessage", scope = "platform", phone = customer, outcome = "Uncertain", tenantId = tenant.ToString() },
                 })
            await Assert.ThrowsAsync<InvalidOperationException>(() => OrderAsync(host, bad, tenant));
    }

    [Fact]
    public async Task Platform_gets_a_minimal_consultation_with_its_reply_unconfirmed_without_turning_consultations_on()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), settings: new Dictionary<string, string>
        {
            ["WhatsApp:PhoneNumberId"] = WhatsAppAttentionJourney.NewNumberId(),
            ["WhatsApp:AccessToken"] = "configured-token",
        });
        using var attendant = await OperatorAsync(host);
        var number = await JsonAsync(attendant.GetAsync($"{Platform}/number", Ct));
        Assert.Equal((JsonValueKind.Object, false), (number.GetProperty("number").ValueKind, number.GetProperty("consultationsEnabled").GetBoolean()));
        const string person = "+5491134567872";
        var order = new { kind = "chatMessage", scope = "platform", phone = person, outcome = "Uncertain" };

        var result = await OrderAsync(host, order, Guid.NewGuid());
        var (chat, reply) = (result.GetProperty("chatId").GetGuid(), result.GetProperty("messageId").GetGuid());
        var conversation = await JsonAsync(attendant.GetAsync($"{Platform}/conversations/{chat}", Ct));
        var messages = conversation.GetProperty("messages").GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(messages, message => message.GetProperty("direction").GetString() == "Inbound"
            && message.GetProperty("text").GetString() == WhatsAppDeliveryControl.ConsultationText);
        var sent = Message(conversation, reply);
        Assert.Equal((WhatsAppDeliveryControl.ConsultationReply, "Uncertain"), (sent.GetProperty("text").GetString(), sent.GetProperty("status").GetString()));
        Assert.Contains((await JsonAsync(attendant.GetAsync($"{Platform}/conversations", Ct))).GetProperty("conversations").GetProperty("items").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == chat);
        (await JsonAsync(attendant.PostAsync($"{Platform}/conversations/{chat}/messages/{reply}/review", null, Ct))).GetProperty("reviewedAtUtc").GetDateTime();

        // Otra vez: la misma consulta y el mismo mensaje, sin la revisión; las consultas siguen apagadas.
        result = await OrderAsync(host, order, Guid.NewGuid());
        Assert.Equal((chat, reply), (result.GetProperty("chatId").GetGuid(), result.GetProperty("messageId").GetGuid()));
        Assert.Equal(JsonValueKind.Null, Message(await JsonAsync(attendant.GetAsync($"{Platform}/conversations/{chat}", Ct)), reply)
            .GetProperty("reviewedAtUtc").ValueKind);
        Assert.False((await JsonAsync(attendant.GetAsync($"{Platform}/number", Ct))).GetProperty("consultationsEnabled").GetBoolean());
    }

    private static async Task<JsonElement> OrderAsync(WebApplicationFactory<Program> host, object command, Guid businessId) =>
        JsonSerializer.SerializeToElement(await WhatsAppDeliveryControl.ExecuteAsync(host.Services, JsonSerializer.SerializeToElement(command), businessId, Ct));

    private static async Task<Guid> SendAsync(HttpClient client, string path, string text)
    {
        using var response = await WhatsAppAttentionJourney.PostAsync(client, path, new { text }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await WhatsAppAttentionJourney.JsonAsync(response, Ct)).GetProperty("id").GetGuid();
    }

    private static JsonElement Message(JsonElement conversation, Guid id) =>
        conversation.GetProperty("messages").GetProperty("items").EnumerateArray().Single(message => message.GetProperty("id").GetGuid() == id);

    private static async Task<JsonElement> JsonAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private static async Task<HttpClient> OperatorAsync(WebApplicationFactory<Program> host)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.WhatsApp.Manage, PlatformPermissions.WhatsApp.Attend], Ct);
        await using (var naming = host.Services.CreateAsyncScope())
            await naming.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.Where(user => user.Id == account.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DisplayName, "Ana Gómez"), Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }
}
