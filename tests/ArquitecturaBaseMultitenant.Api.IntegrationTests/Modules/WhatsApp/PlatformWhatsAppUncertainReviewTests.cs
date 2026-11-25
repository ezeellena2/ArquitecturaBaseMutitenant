using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B7 (ADR 0066 §2) en las consultas de Plataforma: lo revisa quien tiene <c>platform.whatsapp.attend</c>, una vez y con
/// quién y cuándo; sin ese permiso es 403 y lo de otra consulta, 404. El bot y el operador contestan con prioridad, cada
/// consulta en su orden. Base propia: el número de Plataforma es global.
/// </summary>
public sealed class PlatformWhatsAppUncertainReviewTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Platform = "/api/platform/whatsapp";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Operators_who_attend_review_an_unconfirmed_reply_once()
    {
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), settings: new Dictionary<string, string>
        {
            ["WhatsApp:PhoneNumberId"] = phoneNumberId,
            ["WhatsApp:AccessToken"] = "configured-token",
        });
        using var manager = await OperatorAsync(host, "Ana Gómez", PlatformPermissions.WhatsApp.Manage, PlatformPermissions.WhatsApp.Attend);
        using var attendant = await OperatorAsync(host, "Beto Paz", PlatformPermissions.WhatsApp.Attend);
        using var managerOnly = await OperatorAsync(host, "Caro Ruiz", PlatformPermissions.WhatsApp.Manage);
        var number = (await JsonAsync(manager.GetAsync($"{Platform}/number", Ct))).GetProperty("number");
        using (var on = await manager.PutAsJsonAsync($"{Platform}/number",
            new { consultationsEnabled = true, version = number.GetProperty("version").GetUInt32() }, Ct))
            Assert.Equal(HttpStatusCode.OK, on.StatusCode);

        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var (person, other) = (WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone());
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, person, "Necesito ayuda", Ct);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, other, "Hola", Ct);
        var list = await JsonAsync(attendant.GetAsync($"{Platform}/conversations", Ct));
        var (chat, otherChat) = (Consultation(list, person), Consultation(list, other));

        using (var taken = await attendant.PostAsync($"{Platform}/conversations/{chat}/take", null, Ct))
            Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        var first = await SendAsync(attendant, chat, "Te ayudo");
        var second = await SendAsync(attendant, chat, "¿Seguís ahí?");

        // El bot que dio la bienvenida y el operador contestan con prioridad, en la misma fila que la consulta.
        var bot = Messages(await JsonAsync(attendant.GetAsync($"{Platform}/conversations/{chat}", Ct)))
            .Single(message => message.GetProperty("direction").GetString() == "Outbound" && message.GetProperty("author").ValueKind == JsonValueKind.Null)
            .GetProperty("id").GetGuid();
        var rows = new[] { await QueuedAsync(host, bot), await QueuedAsync(host, first), await QueuedAsync(host, second) };
        Assert.All(rows, row => Assert.Equal(OutboxPriority.Reply, row.Priority));
        Assert.Single(rows.Select(row => row.OrderingKey).Distinct());
        Assert.NotNull(rows[0].OrderingKey);

        await OutboxRows.SetStatusAsync(host.Services, rows[1].Id, OutboxStatus.Uncertain, Ct);
        Assert.Equal("Uncertain", Message(await JsonAsync(attendant.GetAsync($"{Platform}/conversations/{chat}", Ct)), first)
            .GetProperty("status").GetString());

        await ProblemAsync(managerOnly.PostAsync(Review(chat, first), null, Ct), HttpStatusCode.Forbidden, null);
        await ProblemAsync(attendant.PostAsync(Review(otherChat, first), null, Ct), HttpStatusCode.NotFound, WhatsAppErrors.ConversationNotFoundCode);
        await ProblemAsync(attendant.PostAsync(Review(Guid.CreateVersion7(), first), null, Ct), HttpStatusCode.NotFound,
            WhatsAppErrors.ConversationNotFoundCode);
        await ProblemAsync(attendant.PostAsync(Review(chat, second), null, Ct), HttpStatusCode.Conflict, WhatsAppErrors.MessageNotUncertainCode);

        // Revisa otro operador que atiende, sin tomar la consulta; la segunda revisión devuelve la primera.
        var reviewed = await JsonAsync(manager.PostAsync(Review(chat, first), null, Ct));
        Assert.Equal(("Uncertain", "Ana Gómez"), (reviewed.GetProperty("status").GetString(), reviewed.GetProperty("reviewedByName").GetString()));
        var again = await JsonAsync(attendant.PostAsync(Review(chat, first), null, Ct));
        Assert.Equal(("Ana Gómez", reviewed.GetProperty("reviewedAtUtc").GetDateTime()),
            (again.GetProperty("reviewedByName").GetString(), again.GetProperty("reviewedAtUtc").GetDateTime()));
        var listed = Message(await JsonAsync(attendant.GetAsync($"{Platform}/conversations/{chat}", Ct)), first);
        Assert.Equal("Ana Gómez", listed.GetProperty("reviewedByName").GetString());
    }

    private static string Review(Guid chat, Guid message) => $"{Platform}/conversations/{chat}/messages/{message}/review";

    private static async Task<Guid> SendAsync(HttpClient client, Guid chat, string text)
    {
        using var response = await WhatsAppAttentionJourney.PostAsync(client, $"{Platform}/conversations/{chat}/messages", new { text }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await WhatsAppAttentionJourney.JsonAsync(response, Ct)).GetProperty("id").GetGuid();
    }

    private static Guid Consultation(JsonElement list, string waId) => list.GetProperty("conversations").GetProperty("items").EnumerateArray()
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

    private static async Task<OutboxMessage> QueuedAsync(WebApplicationFactory<Program> host, Guid message)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = (await context.Set<PlatformWhatsAppChatMessage>().AsNoTracking().SingleAsync(value => value.Id == message, Ct)).OutboxMessageId!.Value;
        return await context.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == id, Ct);
    }

    private static async Task<HttpClient> OperatorAsync(WebApplicationFactory<Program> host, string name, params string[] permissions)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, permissions, Ct);
        await using (var naming = host.Services.CreateAsyncScope())
            await naming.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.Where(user => user.Id == account.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DisplayName, name), Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }
}
