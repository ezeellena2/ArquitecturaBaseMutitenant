using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B4 de la E11: equipos de miembros activos con nombre único por organización; bandejas donde el primero que toma gana y
/// el segundo recibe TakenByOther sin que su mensaje salga; notas que nunca van a Meta; pasar a un equipo; conteos de la
/// bandeja entera (CC15); y sin responder pasada la ventana de 24 h o con el número desconectado (D7).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppInboxTests(ApiFactory factory)
{
    private const string Customers = "/api/organization/whatsapp/customers";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Teams_keep_active_members_with_a_unique_name_and_another_organization_does_not_see_them()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var options = await JsonAsync(await client.GetAsync("/api/organization/whatsapp/teams/member-options", Ct));
        var owner = Assert.Single(options.GetProperty("items").EnumerateArray());
        var ownerMember = owner.GetProperty("id").GetGuid();

        using var created = await WhatsAppAttentionJourney.PostAsync(client, "/api/organization/whatsapp/teams",
            new { name = "Ventas", inbox = "Customers", memberIds = new[] { ownerMember }, notifyAfterMinutes = 15 }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var team = await JsonAsync(created);
        Assert.Equal(1, team.GetProperty("memberCount").GetInt32());
        Assert.Equal([owner.GetProperty("name").GetString()], team.GetProperty("memberPreview").EnumerateArray().Select(name => name.GetString()));
        var teamId = team.GetProperty("id").GetGuid();

        using var duplicate = await WhatsAppAttentionJourney.PostAsync(client, "/api/organization/whatsapp/teams",
            new { name = "ventas", inbox = "Internal", memberIds = Array.Empty<Guid>() }, Ct);
        Assert.Equal((HttpStatusCode.Conflict, WhatsAppErrors.TeamNameTakenCode), (duplicate.StatusCode, await CodeAsync(duplicate)));
        using var withoutInbox = await WhatsAppAttentionJourney.PostAsync(client, "/api/organization/whatsapp/teams",
            new { name = "Sin bandeja", memberIds = Array.Empty<Guid>() }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutInbox.StatusCode);
        using var stranger = await WhatsAppAttentionJourney.PostAsync(client, "/api/organization/whatsapp/teams",
            new { name = "Extraños", inbox = "Customers", memberIds = new[] { Guid.NewGuid() } }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, stranger.StatusCode);

        var detail = await JsonAsync(await client.GetAsync($"/api/organization/whatsapp/teams/{teamId}", Ct));
        Assert.Equal([ownerMember], detail.GetProperty("members").EnumerateArray().Select(member => member.GetProperty("id").GetGuid()));
        var version = team.GetProperty("version").GetUInt32();
        using var updated = await client.PutAsJsonAsync($"/api/organization/whatsapp/teams/{teamId}",
            new { name = "Ventas mayoristas", memberIds = Array.Empty<Guid>(), notifyAfterMinutes = (int?)null, version }, Ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var stale = await client.PutAsJsonAsync($"/api/organization/whatsapp/teams/{teamId}",
            new { name = "Otra vez", memberIds = Array.Empty<Guid>(), version }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var other = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, other, Ct);
        using var foreign = await other.GetAsync($"/api/organization/whatsapp/teams/{teamId}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        var current = (await JsonAsync(updated)).GetProperty("version").GetUInt32();
        using var deleted = await client.DeleteAsync($"/api/organization/whatsapp/teams/{teamId}?version={current}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var gone = await client.GetAsync($"/api/organization/whatsapp/teams/{teamId}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task The_first_to_take_wins_the_second_cannot_send_notes_never_leave_and_counts_cover_the_whole_inbox()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct)).Dispose();
        using var agent = await AgentAsync(host, tenant, Permissions.WhatsApp.Customers);
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var customer = WhatsAppAttentionJourney.NewPhone();
        var other = WhatsAppAttentionJourney.NewPhone();
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, "Hola", Ct);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, other, "Buenas", Ct);

        var list = await JsonAsync(await owner.GetAsync($"{Customers}/conversations", Ct));
        Assert.Equal((2, 0, 2), Counts(list));
        var chat = Row(list, customer);
        var otherChat = Row(list, other);
        Assert.Equal(phoneNumberId, (await ChannelAsync(host, chat.GetProperty("channel").GetProperty("id").GetGuid())).PhoneNumberId);

        using (var taken = await owner.PostAsync($"{Customers}/conversations/{Id(chat)}/take", null, Ct))
            Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        var outboxBefore = await OutboxCountAsync(host);
        using (var blocked = await WhatsAppAttentionJourney.PostAsync(agent, $"{Customers}/conversations/{Id(chat)}/messages", new { text = "Soy otra persona" }, Ct))
        {
            Assert.Equal((HttpStatusCode.Conflict, WhatsAppErrors.ConversationTakenByOtherCode), (blocked.StatusCode, await CodeAsync(blocked)));
            Assert.Equal(JsonValueKind.String, (await WhatsAppAttentionJourney.JsonAsync(blocked, Ct)).GetProperty("takenBy").ValueKind);
        }
        Assert.Equal(outboxBefore, await OutboxCountAsync(host));

        // Dos que toman a la vez: gana uno y el otro recibe TakenByOther.
        var race = await Task.WhenAll(owner.PostAsync($"{Customers}/conversations/{Id(otherChat)}/take", null, Ct),
            agent.PostAsync($"{Customers}/conversations/{Id(otherChat)}/take", null, Ct));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], race.Select(response => response.StatusCode).Order());
        foreach (var response in race) response.Dispose();

        using (var sent = await WhatsAppAttentionJourney.PostAsync(owner, $"{Customers}/conversations/{Id(chat)}/messages", new { text = "Te ayudo" }, Ct))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
            var message = await JsonAsync(sent);
            Assert.Equal(("Text", "Outbound", "Pending"), (message.GetProperty("kind").GetString(), message.GetProperty("direction").GetString(),
                message.GetProperty("status").GetString()));
        }
        Assert.Equal(outboxBefore + 1, await OutboxCountAsync(host));
        using (var note = await WhatsAppAttentionJourney.PostAsync(owner, $"{Customers}/conversations/{Id(chat)}/notes", new { text = "Nota interna" }, Ct))
            Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        Assert.Equal(outboxBefore + 1, await OutboxCountAsync(host));

        var conversation = await JsonAsync(await owner.GetAsync($"{Customers}/conversations/{Id(chat)}", Ct));
        var messages = conversation.GetProperty("messages").GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(("Note", "Nota interna"), (messages[0].GetProperty("kind").GetString(), messages[0].GetProperty("text").GetString()));
        Assert.True(conversation.GetProperty("conversation").GetProperty("assignedToMe").GetBoolean());

        using var team = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/teams",
            new { name = "Ventas", inbox = "Customers", memberIds = Array.Empty<Guid>() }, Ct);
        var teamId = (await JsonAsync(team)).GetProperty("id").GetGuid();
        using (var transferred = await WhatsAppAttentionJourney.PostAsync(owner, $"{Customers}/conversations/{Id(chat)}/transfer",
            new { teamId, version = conversation.GetProperty("conversation").GetProperty("version").GetUInt32() }, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, transferred.StatusCode);
            var row = await JsonAsync(transferred);
            Assert.Equal(teamId, row.GetProperty("team").GetProperty("id").GetGuid());
            Assert.Equal(JsonValueKind.Null, row.GetProperty("assignee").ValueKind);
        }

        // CC15: búsqueda, estado y equipo filtran la página, no los conteos; quien solo atiende clientes ve 0 en Internos.
        var filtered = await JsonAsync(await owner.GetAsync($"{Customers}/conversations?search=zzz&state=Mine&teamId={teamId}", Ct));
        Assert.Equal(0, filtered.GetProperty("conversations").GetProperty("totalCount").GetInt32());
        Assert.Equal((2, 0, 1), Counts(filtered));
        Assert.Equal((2, 0, 1), Counts(await JsonAsync(await agent.GetAsync($"{Customers}/conversations", Ct))));
        using var forbidden = await agent.GetAsync("/api/organization/whatsapp/internal/conversations", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var untaken = await JsonAsync(await agent.GetAsync("/api/organization/whatsapp/untaken", Ct));
        Assert.Equal((1, JsonValueKind.Null), (untaken.GetProperty("customers").GetInt32(), untaken.GetProperty("internal").ValueKind));
    }

    [Fact]
    public async Task Nobody_replies_after_the_24_hour_window_or_once_the_number_is_disconnected()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct);
        var number = await JsonAsync(connected);
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var late = WhatsAppAttentionJourney.NewPhone();
        var recent = WhatsAppAttentionJourney.NewPhone();
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, late, "Hola", Ct, TimeProvider.System.GetUtcNow().AddHours(-25));
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, recent, "Hola", Ct);
        var list = await JsonAsync(await owner.GetAsync($"{Customers}/conversations", Ct));
        var lateChat = Row(list, late);
        Assert.Equal(JsonValueKind.Null, lateChat.GetProperty("windowOpenUntilUtc").ValueKind);

        using (var closed = await WhatsAppAttentionJourney.PostAsync(owner, $"{Customers}/conversations/{Id(lateChat)}/messages", new { text = "Hola" }, Ct))
            Assert.Equal((HttpStatusCode.Conflict, WhatsAppErrors.ConversationWindowClosedCode), (closed.StatusCode, await CodeAsync(closed)));

        using (var disconnected = await owner.DeleteAsync($"/api/organization/whatsapp/numbers/{Id(number)}?version={number.GetProperty("version").GetUInt32()}", Ct))
            Assert.Equal(HttpStatusCode.NoContent, disconnected.StatusCode);
        var recentChat = Row(await JsonAsync(await owner.GetAsync($"{Customers}/conversations", Ct)), recent);
        Assert.True(recentChat.GetProperty("readOnly").GetBoolean());
        using var readOnly = await WhatsAppAttentionJourney.PostAsync(owner, $"{Customers}/conversations/{Id(recentChat)}/messages", new { text = "Hola" }, Ct);
        Assert.Equal((HttpStatusCode.Conflict, WhatsAppErrors.ConversationReadOnlyCode), (readOnly.StatusCode, await CodeAsync(readOnly)));
    }

    /// <summary>Una persona que entra como empresa y es miembro con un rol que da esos permisos.</summary>
    private async Task<HttpClient> AgentAsync(WebApplicationFactory<Program> host, Guid tenant, params string[] permissions)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
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

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using (response) return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await WhatsAppAttentionJourney.JsonAsync(response, Ct)).GetProperty("code").GetString();

    private static (int Customers, int Internal, int Untaken) Counts(JsonElement list)
    {
        var counts = list.GetProperty("counts");
        return (counts.GetProperty("customers").GetInt32(), counts.GetProperty("internal").GetInt32(), counts.GetProperty("untaken").GetInt32());
    }

    private static JsonElement Row(JsonElement list, string waId) =>
        list.GetProperty("conversations").GetProperty("items").EnumerateArray().Single(row => row.GetProperty("displayNumber").GetString() == waId);

    private static Guid Id(JsonElement element) => element.GetProperty("id").GetGuid();

    private static async Task<WhatsAppChannel> ChannelAsync(WebApplicationFactory<Program> host, Guid id)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChannel>().AsNoTracking().SingleAsync(value => value.Id == id, Ct);
    }

    private static async Task<int> OutboxCountAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.CountAsync(value => value.Channel == "whatsapp", Ct);
    }
}
