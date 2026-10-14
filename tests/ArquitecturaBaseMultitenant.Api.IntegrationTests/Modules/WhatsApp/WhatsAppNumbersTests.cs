using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>B1 de la E11: números de la organización, alta embebida, medio de pago, contador del mes y aislamiento.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppNumbersTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Connecting_is_idempotent_lists_the_month_and_never_exposes_the_token_or_the_code()
    {
        var graph = new FakeGraph(); var logs = new CapturedLogs();
        await using var host = WhatsAppAttentionJourney.Host(factory, graph, logs);
        using var client = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId(); var key = Guid.NewGuid().ToString();

        using var created = await WhatsAppAttentionJourney.ConnectAsync(client, number, Ct, key);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var body = await created.Content.ReadAsStringAsync(Ct);
        using var replayed = await WhatsAppAttentionJourney.ConnectAsync(client, number, Ct, key);
        Assert.Equal(HttpStatusCode.Created, replayed.StatusCode);
        Assert.Equal("true", replayed.Headers.GetValues("Idempotent-Replayed").Single());
        using var again = await WhatsAppAttentionJourney.ConnectAsync(client, number, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(WhatsAppErrors.NumberAlreadyConnectedCode, (await WhatsAppAttentionJourney.JsonAsync(again, Ct)).GetProperty("code").GetString());
        Assert.Equal(1, graph.Signups);

        using var list = await client.GetAsync("/api/organization/whatsapp/numbers", Ct);
        var listed = await list.Content.ReadAsStringAsync(Ct);
        using var json = JsonDocument.Parse(listed);
        var row = Assert.Single(json.RootElement.GetProperty("numbers").GetProperty("items").EnumerateArray());
        Assert.Equal("Connected", row.GetProperty("status").GetString());
        Assert.Equal(0, row.GetProperty("serviceMessagesThisMonth").GetInt32());
        Assert.Equal(1000, row.GetProperty("freeServiceMessages").GetInt32());
        Assert.Equal("config-fixture", json.RootElement.GetProperty("signup").GetProperty("configId").GetString());
        foreach (var text in new[] { body, listed })
        {
            Assert.DoesNotContain(WhatsAppAttentionJourney.Token, text, StringComparison.Ordinal);
            Assert.DoesNotContain("123456", text, StringComparison.Ordinal);
        }
        Assert.DoesNotContain(logs.Entries, entry => entry.Contains(WhatsAppAttentionJourney.Token, StringComparison.Ordinal)
            || entry.Contains("signup-code-", StringComparison.Ordinal));
        await using var scope = host.Services.CreateAsyncScope();
        var channel = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChannel>().AsNoTracking()
            .SingleAsync(value => value.PhoneNumberId == number, Ct);
        Assert.DoesNotContain(WhatsAppAttentionJourney.Token, Encoding.UTF8.GetString(channel.EncryptedToken), StringComparison.Ordinal);
        Assert.NotNull(channel.EncryptedPin);
        Assert.True(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SecurityEvents.AnyAsync(value =>
            value.Type == SecurityEventType.TenantFeaturesChanged && value.TargetTenantId == channel.OrganizationId
            && value.ActorKind == AuditActorKind.User, Ct));
    }

    [Fact]
    public async Task A_number_of_another_organization_conflicts_and_stays_invisible()
    {
        var graph = new FakeGraph();
        await using var host = WhatsAppAttentionJourney.Host(factory, graph);
        using var first = WhatsAppAttentionJourney.Client(host);
        using var second = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, first, Ct);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, second, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId();
        using var created = await WhatsAppAttentionJourney.ConnectAsync(first, number, Ct);
        var id = (await WhatsAppAttentionJourney.JsonAsync(created, Ct)).GetProperty("id").GetGuid();

        using var taken = await WhatsAppAttentionJourney.ConnectAsync(second, number, Ct);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        using var foreign = await second.GetAsync("/api/organization/whatsapp/numbers/" + id, Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var foreignDisconnect = await second.DeleteAsync("/api/organization/whatsapp/numbers/" + id + "?version=1", Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreignDisconnect.StatusCode);
        using var list = await second.GetAsync("/api/organization/whatsapp/numbers", Ct);
        Assert.Equal(0, (await WhatsAppAttentionJourney.JsonAsync(list, Ct)).GetProperty("numbers").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task A_number_without_payment_shows_in_home_and_disconnecting_leaves_it_out()
    {
        var graph = new FakeGraph();
        await using var host = WhatsAppAttentionJourney.Host(factory, graph);
        using var client = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        using var created = await WhatsAppAttentionJourney.ConnectAsync(client, WhatsAppAttentionJourney.NewNumberId(), Ct);
        var number = await WhatsAppAttentionJourney.JsonAsync(created, Ct);
        var id = number.GetProperty("id").GetGuid();

        graph.HasPayment = false;
        using var checkedNumber = await WhatsAppAttentionJourney.PostAsync(client, $"/api/organization/whatsapp/numbers/{id}/check",
            new { version = number.GetProperty("version").GetUInt32() }, Ct);
        var after = await WhatsAppAttentionJourney.JsonAsync(checkedNumber, Ct);
        Assert.Equal("PaymentMissing", after.GetProperty("status").GetString());
        using var stale = await WhatsAppAttentionJourney.PostAsync(client, $"/api/organization/whatsapp/numbers/{id}/check",
            new { version = number.GetProperty("version").GetUInt32() }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var home = await OrganizationJourney.HomeAsync(client, Ct);
        var group = Assert.Single(home.GetProperty("pendingGroups").EnumerateArray(), value => value.GetProperty("area").GetString() == "whatsapp");
        Assert.Equal("numberPaymentMissing", group.GetProperty("kind").GetString());
        var card = Assert.Single(home.GetProperty("moduleCards").EnumerateArray());
        Assert.Equal(WhatsAppFeature.Key, card.GetProperty("key").GetString());
        Assert.Equal(1, card.GetProperty("total").GetInt32());

        using var disconnected = await client.DeleteAsync($"/api/organization/whatsapp/numbers/{id}?version={after.GetProperty("version").GetUInt32()}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, disconnected.StatusCode);
        using var gone = await client.GetAsync("/api/organization/whatsapp/numbers/" + id, Ct);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        home = await OrganizationJourney.HomeAsync(client, Ct);
        Assert.DoesNotContain(home.GetProperty("pendingGroups").EnumerateArray(), value => value.GetProperty("area").GetString() == "whatsapp");
    }

    [Fact]
    public async Task Delivered_service_messages_count_once_and_payment_errors_mark_the_number()
    {
        var graph = new FakeGraph();
        await using var host = WhatsAppAttentionJourney.Host(factory, graph);
        using var client = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using var created = await WhatsAppAttentionJourney.ConnectAsync(client, phoneNumberId, Ct);
        var id = (await WhatsAppAttentionJourney.JsonAsync(created, Ct)).GetProperty("id").GetGuid();
        using var anonymous = WhatsAppAttentionJourney.Client(host);

        using var first = await WhatsAppAttentionJourney.WebhookAsync(anonymous, WhatsAppAttentionJourney.Statuses(phoneNumberId,
            WhatsAppAttentionJourney.Status("wamid.free", "delivered", "service", "free_customer_service"),
            WhatsAppAttentionJourney.Status("wamid.free", "read", "service", "free_customer_service"),
            WhatsAppAttentionJourney.Status("wamid.paid", "read", "service", "regular"),
            WhatsAppAttentionJourney.Status("wamid.template", "delivered", "utility", "regular")), Ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var late = await WhatsAppAttentionJourney.WebhookAsync(anonymous, WhatsAppAttentionJourney.Statuses(phoneNumberId,
            WhatsAppAttentionJourney.Status("wamid.paid", "delivered", "service", "regular")), Ct);
        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        using var counted = await client.GetAsync("/api/organization/whatsapp/numbers/" + id, Ct);
        var row = await WhatsAppAttentionJourney.JsonAsync(counted, Ct);
        Assert.Equal(2, row.GetProperty("serviceMessagesThisMonth").GetInt32());
        Assert.Equal(1, row.GetProperty("chargedServiceMessagesThisMonth").GetInt32());
        Assert.Equal("Connected", row.GetProperty("status").GetString());

        using var failed = await WhatsAppAttentionJourney.WebhookAsync(anonymous, WhatsAppAttentionJourney.Statuses(phoneNumberId,
            WhatsAppAttentionJourney.Status("wamid.marketing", "failed", error: 131042)), Ct);
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        using var missing = await client.GetAsync("/api/organization/whatsapp/numbers/" + id, Ct);
        Assert.Equal("PaymentMissing", (await WhatsAppAttentionJourney.JsonAsync(missing, Ct)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_member_without_the_permission_gets_403_and_a_company_of_another_organization_is_not_found()
    {
        var graph = new FakeGraph();
        await using var host = WhatsAppAttentionJourney.Host(factory, graph);
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        using var other = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, other, Ct);
        var foreignCompany = await OrganizationJourney.CreateCompanyAsync(other, "Ajena", Ct);
        using var foreign = await WhatsAppAttentionJourney.ConnectAsync(owner, WhatsAppAttentionJourney.NewNumberId(), Ct, companyId: foreignCompany);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(0, graph.Signups);

        using var member = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, member, Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenant, account.UserId, Ct);
        await AccountJourney.AuthorizeAsync(member, "business", Ct, tenant);
        using var forbidden = await member.GetAsync("/api/organization/whatsapp/numbers", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
