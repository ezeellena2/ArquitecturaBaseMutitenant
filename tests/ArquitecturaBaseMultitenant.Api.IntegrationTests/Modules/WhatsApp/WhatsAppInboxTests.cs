using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

    [Fact]
    public async Task A_reply_to_an_operator_template_stays_with_the_operator_while_an_old_template_or_broadcast_restarts_with_the_bot()
    {
        var templates = new FakeTemplates();
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph(), templates: templates);
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (ownerAccount, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        var wabaId = "waba-" + phoneNumberId;
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct))
            Assert.Equal(HttpStatusCode.Created, connected.StatusCode);

        var salesTeamId = await CreateTeamAsync(owner, "Ventas", "Customers");
        var followUpId = await CreateApprovedTemplateAsync(owner, wabaId, "Seguimiento", "seguimiento", "Utility",
            "Hola {{1}}, seguimos tu consulta.", [new { label = "nombre", example = "Ana" }]);
        var promoId = await CreateApprovedTemplateAsync(owner, wabaId, "Promo semanal", "promo_semanal", "Marketing",
            "Hola, esta semana tenés {{1}}.", [new { label = "oferta", example = "20%" }]);

        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var recent = WhatsAppAttentionJourney.NewPhone();
        var oldTemplate = WhatsAppAttentionJourney.NewPhone();
        var broadcastCustomer = WhatsAppAttentionJourney.NewPhone();
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, recent, "Hola 1", Ct, TimeProvider.System.GetUtcNow().AddHours(-25));
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, oldTemplate, "Hola 2", Ct, TimeProvider.System.GetUtcNow().AddHours(-31));
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, broadcastCustomer, "Hola 3", Ct, TimeProvider.System.GetUtcNow().AddHours(-25));

        var list = await JsonAsync(await owner.GetAsync($"{Customers}/conversations", Ct));
        var recentChat = Row(list, recent);
        var oldChat = Row(list, oldTemplate);
        var broadcastChat = Row(list, broadcastCustomer);

        // Caso 1: plantilla reciente de un operador con ventana cerrada → sigue tomado por él en su equipo y sin bienvenida.
        await TransferToTeamAsync(owner, Customers, recentChat, salesTeamId);
        using (var sentTemplate = await WhatsAppAttentionJourney.PostAsync(owner, $"{Customers}/conversations/{Id(recentChat)}/templates",
            new { templateId = followUpId, variables = new[] { "Ana" } }, Ct))
            Assert.Equal(HttpStatusCode.Created, sentTemplate.StatusCode);
        var lastConversationBefore = await LastConversationAtAsync(host, tenant, recent);
        var outboxBeforeRecent = await OutboxCountAsync(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, recent, "Sí, seguimos", Ct);

        var recentDetail = await JsonAsync(await owner.GetAsync($"{Customers}/conversations/{Id(recentChat)}", Ct));
        var recentAfter = recentDetail.GetProperty("conversation");
        Assert.True(recentAfter.GetProperty("assignedToMe").GetBoolean());
        Assert.Equal(salesTeamId, recentAfter.GetProperty("team").GetProperty("id").GetGuid());
        Assert.NotEqual(JsonValueKind.Null, recentAfter.GetProperty("windowOpenUntilUtc").ValueKind);
        Assert.Equal(outboxBeforeRecent, await OutboxCountAsync(host));
        Assert.True(await LastConversationAtAsync(host, tenant, recent) > lastConversationBefore);

        // Caso 2: plantilla de un operador de hace 30 h → reinicia con el bot.
        await TransferToTeamAsync(owner, Customers, oldChat, salesTeamId);
        using (var takenOld = await owner.PostAsync($"{Customers}/conversations/{Id(oldChat)}/take", null, Ct))
            Assert.Equal(HttpStatusCode.OK, takenOld.StatusCode);
        await SeedOperatorTemplateAsync(host, tenant, Id(oldChat), ownerAccount.UserId, followUpId,
            TimeProvider.System.GetUtcNow().UtcDateTime.AddHours(-30));
        var outboxBeforeOld = await OutboxCountAsync(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, oldTemplate, "Volví", Ct);

        var oldAfter = (await JsonAsync(await owner.GetAsync($"{Customers}/conversations/{Id(oldChat)}", Ct))).GetProperty("conversation");
        Assert.Equal(JsonValueKind.Null, oldAfter.GetProperty("assignee").ValueKind);
        Assert.Equal(JsonValueKind.Null, oldAfter.GetProperty("team").ValueKind);
        Assert.True(await OutboxCountAsync(host) > outboxBeforeOld);

        // Caso 3: difusión reciente → no cuenta como plantilla de operador y reinicia con el bot.
        await TransferToTeamAsync(owner, Customers, broadcastChat, salesTeamId);
        using (var takenBroadcast = await owner.PostAsync($"{Customers}/conversations/{Id(broadcastChat)}/take", null, Ct))
            Assert.Equal(HttpStatusCode.OK, takenBroadcast.StatusCode);
        await AcceptPromotionsAsync(owner, broadcastCustomer);
        using (var broadcast = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/broadcasts", new
        {
            name = "Difusión B11",
            templateId = promoId,
            variables = new[] { "20%" },
            audience = new { kind = "AllAccepting" },
            scheduledAtUtc = (DateTime?)null,
        }, Ct))
            Assert.Equal(HttpStatusCode.Created, broadcast.StatusCode);
        await host.Services.GetServices<IHostedService>().OfType<WhatsAppBroadcastWorker>().Single().RunOnceAsync(Ct);
        var outboxBeforeBroadcast = await OutboxCountAsync(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, broadcastCustomer, "Quiero la promo", Ct);

        var broadcastAfter = (await JsonAsync(await owner.GetAsync($"{Customers}/conversations/{Id(broadcastChat)}", Ct))).GetProperty("conversation");
        Assert.Equal(JsonValueKind.Null, broadcastAfter.GetProperty("assignee").ValueKind);
        Assert.Equal(JsonValueKind.Null, broadcastAfter.GetProperty("team").ValueKind);
        Assert.True(await OutboxCountAsync(host) > outboxBeforeBroadcast);
    }

    [Fact]
    public async Task Losing_access_or_inbox_permission_releases_only_the_chats_the_member_can_no_longer_attend_keeping_their_team()
    {
        const string internals = "/api/organization/whatsapp/internal";
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct))
            Assert.Equal(HttpStatusCode.Created, connected.StatusCode);

        var salesTeamId = await CreateTeamAsync(owner, "Ventas", "Customers");
        var internalTeamId = await CreateTeamAsync(owner, "Internos", "Internal");
        var customersRoleId = await CreateOrgRoleAsync(owner, "Solo clientes", Permissions.WhatsApp.Customers);
        var bothRoleId = await CreateOrgRoleAsync(owner, "Ambas bandejas", Permissions.WhatsApp.Customers, Permissions.WhatsApp.Internal);
        var backupCustomersRoleId = await CreateOrgRoleAsync(owner, "Respaldo clientes", Permissions.WhatsApp.Customers);
        var auditOnlyRoleId = await CreateOrgRoleAsync(owner, "Solo auditoría", Permissions.Audit.Read);

        var suspended = await AgentWithRolesAsync(host, tenant, customersRoleId);
        var roleRemoved = await AgentWithRolesAsync(host, tenant, customersRoleId);
        var roleEdited = await AgentWithRolesAsync(host, tenant, bothRoleId);
        var keepsPermission = await AgentWithRolesAsync(host, tenant, bothRoleId, backupCustomersRoleId);
        var removed = await AgentWithRolesAsync(host, tenant, customersRoleId);
        using var _1 = suspended.Client;
        using var _2 = roleRemoved.Client;
        using var _3 = roleEdited.Client;
        using var _4 = keepsPermission.Client;
        using var _5 = removed.Client;

        var employeePhone = WhatsAppAttentionJourney.NewPhone();
        await AddActiveMemberWithPhoneAsync(tenant, employeePhone);

        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var phones = Enumerable.Range(0, 8).Select(_ => WhatsAppAttentionJourney.NewPhone()).ToArray();
        foreach (var phone in phones) await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Hola", Ct);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, employeePhone, "Consulta interna", Ct);

        var customerList = await JsonAsync(await owner.GetAsync($"{Customers}/conversations?pageSize=20", Ct));
        var internalList = await JsonAsync(await owner.GetAsync($"{internals}/conversations", Ct));
        var s1 = await AssignAndTakeAsync(owner, suspended.Client, Customers, Row(customerList, phones[0]), salesTeamId);
        var s2 = await AssignAndTakeAsync(owner, suspended.Client, Customers, Row(customerList, phones[1]), salesTeamId);
        var r1 = await AssignAndTakeAsync(owner, roleRemoved.Client, Customers, Row(customerList, phones[2]), salesTeamId);
        var r2 = await AssignAndTakeAsync(owner, roleRemoved.Client, Customers, Row(customerList, phones[3]), salesTeamId);
        var e1 = await AssignAndTakeAsync(owner, roleEdited.Client, Customers, Row(customerList, phones[4]), salesTeamId);
        var e2 = await AssignAndTakeAsync(owner, roleEdited.Client, Customers, Row(customerList, phones[5]), salesTeamId);
        var eInternal = await AssignAndTakeAsync(owner, roleEdited.Client, internals, Row(internalList, employeePhone), internalTeamId);
        var kept = await AssignAndTakeAsync(owner, keepsPermission.Client, Customers, Row(customerList, phones[6]), salesTeamId);
        var d1 = await AssignAndTakeAsync(owner, removed.Client, Customers, Row(customerList, phones[7]), salesTeamId);

        // 1. Suspender miembro → sus dos chats vuelven a «Sin tomar» en su equipo.
        var suspendedVersion = (await JsonAsync(await owner.GetAsync($"/api/users/{suspended.MemberId}", Ct))).GetProperty("version").GetUInt32();
        using (var status = await owner.PutAsJsonAsync($"/api/users/{suspended.MemberId}/status", new { isActive = false, version = suspendedVersion }, Ct))
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        await AssertUntakenInTeamAsync(owner, Customers, s1, salesTeamId);
        await AssertUntakenInTeamAsync(owner, Customers, s2, salesTeamId);

        // 2. Sacarle el rol de WhatsApp → sus dos chats vuelven a «Sin tomar» en su equipo.
        var roleRemovedVersion = (await JsonAsync(await owner.GetAsync($"/api/users/{roleRemoved.MemberId}", Ct))).GetProperty("version").GetUInt32();
        using (var editedUser = await owner.PutAsJsonAsync($"/api/users/{roleRemoved.MemberId}",
            new { roleIds = new[] { auditOnlyRoleId }, companies = Array.Empty<object>(), version = roleRemovedVersion }, Ct))
            Assert.Equal(HttpStatusCode.OK, editedUser.StatusCode);
        await AssertUntakenInTeamAsync(owner, Customers, r1, salesTeamId);
        await AssertUntakenInTeamAsync(owner, Customers, r2, salesTeamId);

        // 3. Quitar whatsapp.customers del rol → libera solo Clientes de quien ya no lo tiene; Internos y quien conserva el permiso siguen tomados.
        var bothRole = await JsonAsync(await owner.GetAsync($"/api/roles/{bothRoleId}", Ct));
        using (var editedRole = await owner.PutAsJsonAsync($"/api/roles/{bothRoleId}", new
        {
            name = "Ambas bandejas",
            description = (string?)null,
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = new[] { Permissions.WhatsApp.Internal },
            version = bothRole.GetProperty("version").GetUInt32(),
        }, Ct))
            Assert.Equal(HttpStatusCode.OK, editedRole.StatusCode);
        await AssertUntakenInTeamAsync(owner, Customers, e1, salesTeamId);
        await AssertUntakenInTeamAsync(owner, Customers, e2, salesTeamId);
        await AssertTakenByAsync(owner, internals, eInternal, roleEdited.MemberId, internalTeamId);
        await AssertTakenByAsync(owner, Customers, kept, keepsPermission.MemberId, salesTeamId);

        // 4. Quitar miembro de la organización → su chat vuelve a «Sin tomar» en su equipo.
        var removedVersion = (await JsonAsync(await owner.GetAsync($"/api/users/{removed.MemberId}", Ct))).GetProperty("version").GetUInt32();
        using (var deletedUser = await owner.DeleteAsync($"/api/users/{removed.MemberId}?version={removedVersion}", Ct))
            Assert.Equal(HttpStatusCode.NoContent, deletedUser.StatusCode);
        await AssertUntakenInTeamAsync(owner, Customers, d1, salesTeamId);
    }

    [Fact]
    public async Task Transfer_rejects_a_member_without_the_inbox_permission_and_accepts_one_with_it()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct))
            Assert.Equal(HttpStatusCode.Created, connected.StatusCode);

        var auditOnlyRoleId = await CreateOrgRoleAsync(owner, "Sin WhatsApp", Permissions.Audit.Read);
        var internalOnlyRoleId = await CreateOrgRoleAsync(owner, "Solo internos", Permissions.WhatsApp.Internal);
        var customersRoleId = await CreateOrgRoleAsync(owner, "Atención clientes", Permissions.WhatsApp.Customers);
        var noPermission = await AgentWithRolesAsync(host, tenant, auditOnlyRoleId);
        var internalOnly = await AgentWithRolesAsync(host, tenant, internalOnlyRoleId);
        var customersAgent = await AgentWithRolesAsync(host, tenant, customersRoleId);
        using var _1 = noPermission.Client;
        using var _2 = internalOnly.Client;
        using var _3 = customersAgent.Client;

        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var customer = WhatsAppAttentionJourney.NewPhone();
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, "Hola", Ct);
        var chat = Row(await JsonAsync(await owner.GetAsync($"{Customers}/conversations", Ct)), customer);
        var version = chat.GetProperty("version").GetUInt32();

        foreach (var memberId in new[] { noPermission.MemberId, internalOnly.MemberId })
        {
            using var rejected = await WhatsAppAttentionJourney.PostAsync(owner, $"{Customers}/conversations/{Id(chat)}/transfer",
                new { memberId, version }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            var body = await WhatsAppAttentionJourney.JsonAsync(rejected, Ct);
            Assert.True(body.GetProperty("errors").TryGetProperty("memberId", out _));
        }

        using var accepted = await WhatsAppAttentionJourney.PostAsync(owner, $"{Customers}/conversations/{Id(chat)}/transfer",
            new { memberId = customersAgent.MemberId, version }, Ct);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var row = await JsonAsync(accepted);
        Assert.Equal(customersAgent.MemberId, row.GetProperty("assignee").GetProperty("id").GetGuid());
    }

    private static async Task<Guid> CreateTeamAsync(HttpClient owner, string name, string inbox)
    {
        using var created = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/teams",
            new { name, inbox, memberIds = Array.Empty<Guid>() }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await WhatsAppAttentionJourney.JsonAsync(created, Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOrgRoleAsync(HttpClient owner, string name, params string[] permissions)
    {
        using var created = await AccountJourney.PostAsync(owner, "/api/roles", new
        {
            name,
            description = (string?)null,
            scope = "Organization",
            companyId = (Guid?)null,
            permissions,
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return await created.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    private static async Task<Guid> CreateApprovedTemplateAsync(HttpClient owner, string wabaId, string name, string metaName,
        string category, string body, object[] variables)
    {
        using var created = await WhatsAppAttentionJourney.PostAsync(owner, "/api/organization/whatsapp/templates",
            new { name, category, language = "es-AR", body, variables }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await WhatsAppAttentionJourney.JsonAsync(created, Ct)).GetProperty("id").GetGuid();
        await WhatsAppTemplateManagementTests.TemplateWebhookAsync(owner, wabaId, "message_template_status_update", new
        {
            @event = "APPROVED",
            message_template_id = 1L,
            message_template_name = metaName,
            message_template_language = "es_AR",
            reason = "NONE",
        });
        return id;
    }

    private static async Task TransferToTeamAsync(HttpClient owner, string inboxPath, JsonElement chat, Guid teamId)
    {
        using var transferred = await WhatsAppAttentionJourney.PostAsync(owner, $"{inboxPath}/conversations/{Id(chat)}/transfer",
            new { teamId, version = chat.GetProperty("version").GetUInt32() }, Ct);
        Assert.Equal(HttpStatusCode.OK, transferred.StatusCode);
    }

    private static async Task<Guid> AssignAndTakeAsync(HttpClient owner, HttpClient agent, string inboxPath, JsonElement chat, Guid teamId)
    {
        await TransferToTeamAsync(owner, inboxPath, chat, teamId);
        using var taken = await agent.PostAsync($"{inboxPath}/conversations/{Id(chat)}/take", null, Ct);
        Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        return Id(chat);
    }

    private static async Task AssertUntakenInTeamAsync(HttpClient owner, string inboxPath, Guid chatId, Guid teamId)
    {
        var row = (await JsonAsync(await owner.GetAsync($"{inboxPath}/conversations/{chatId}", Ct))).GetProperty("conversation");
        Assert.Equal(JsonValueKind.Null, row.GetProperty("assignee").ValueKind);
        Assert.Equal(teamId, row.GetProperty("team").GetProperty("id").GetGuid());
    }

    private static async Task AssertTakenByAsync(HttpClient owner, string inboxPath, Guid chatId, Guid memberId, Guid teamId)
    {
        var row = (await JsonAsync(await owner.GetAsync($"{inboxPath}/conversations/{chatId}", Ct))).GetProperty("conversation");
        Assert.Equal(memberId, row.GetProperty("assignee").GetProperty("id").GetGuid());
        Assert.Equal(teamId, row.GetProperty("team").GetProperty("id").GetGuid());
    }

    private static async Task AcceptPromotionsAsync(HttpClient owner, string phone)
    {
        var contact = Assert.Single((await JsonAsync(await owner.GetAsync($"/api/organization/whatsapp/contacts?search={Uri.EscapeDataString(phone)}", Ct)))
            .GetProperty("contacts").GetProperty("items").EnumerateArray());
        using var updated = await owner.PutAsJsonAsync($"/api/organization/whatsapp/contacts/{Id(contact)}",
            new { name = "Cliente", tags = Array.Empty<string>(), consent = "Accepted", version = contact.GetProperty("version").GetUInt32() }, Ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    private static async Task<DateTime?> LastConversationAtAsync(WebApplicationFactory<Program> host, Guid tenantId, string phone)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppCustomer>().AsNoTracking()
            .SingleAsync(value => value.WaId == phone, Ct)).LastConversationAtUtc;
    }

    private static async Task SeedOperatorTemplateAsync(WebApplicationFactory<Program> host, Guid tenantId, Guid chatId, Guid authorUserId,
        Guid templateId, DateTime createdAtUtc)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChatMessage>().Add(
                WhatsAppChatMessage.Outbound(chatId, WhatsAppChatMessageKind.Template, authorUserId, "Dueño", "Hola viejo",
                    Guid.CreateVersion7(), templateId, createdAtUtc));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task AddActiveMemberWithPhoneAsync(Guid tenantId, string phone)
    {
        Guid userId = Guid.Empty;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                userId = (await services.GetRequiredService<IUserRepository>()
                    .CreateAsync("Empleada", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                var method = LoginMethod.CreatePhone(userId, PhoneNumber.Create(phone).Value);
                method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                services.GetRequiredService<ILoginMethodRepository>().Add(method);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, userId, Ct);
    }

    private async Task<(HttpClient Client, Guid MemberId, Guid UserId)> AgentWithRolesAsync(
        WebApplicationFactory<Program> host, Guid tenant, params Guid[] roleIds)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenant, account.UserId, Ct);
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                foreach (var roleId in roleIds)
                    db.Add(RoleAssignment.Assign(memberId, roleId, RoleScope.Organization, null, null).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        return (client, memberId, account.UserId);
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
