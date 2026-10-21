using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class PlatformWhatsAppTeamTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task An_operator_without_a_name_is_identified_by_email_in_options_and_saved_teams()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.WhatsApp.Manage], ct);
        await AccountJourney.AuthorizeAsync(client, "platform", ct);

        var options = await WhatsAppAttentionJourney.JsonAsync(await client.GetAsync(
            "/api/platform/whatsapp/teams/member-options?search=" + Uri.EscapeDataString(account.Email.Value), ct), ct);
        var option = Assert.Single(options.GetProperty("items").EnumerateArray());
        Assert.Equal(account.UserId, option.GetProperty("id").GetGuid());
        Assert.Equal(account.Email.DisplayValue, option.GetProperty("name").GetString());

        using var created = await WhatsAppAttentionJourney.PostAsync(client, "/api/platform/whatsapp/teams",
            new { name = "Equipo " + Guid.NewGuid().ToString("N"), memberIds = new[] { account.UserId }, notifyAfterMinutes = 10 }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var team = await WhatsAppAttentionJourney.JsonAsync(created, ct);
        var id = team.GetProperty("id").GetGuid();
        Assert.Equal(account.Email.DisplayValue, Assert.Single(team.GetProperty("memberPreview").EnumerateArray()).GetString());

        var detail = await WhatsAppAttentionJourney.JsonAsync(await client.GetAsync($"/api/platform/whatsapp/teams/{id}", ct), ct);
        var member = Assert.Single(detail.GetProperty("members").EnumerateArray());
        Assert.Equal(account.UserId, member.GetProperty("id").GetGuid());
        Assert.Equal(account.Email.DisplayValue, member.GetProperty("name").GetString());

        var list = await WhatsAppAttentionJourney.JsonAsync(await client.GetAsync("/api/platform/whatsapp/teams", ct), ct);
        var row = list.GetProperty("items").EnumerateArray().Single(value => value.GetProperty("id").GetGuid() == id);
        Assert.Equal(account.Email.DisplayValue, Assert.Single(row.GetProperty("memberPreview").EnumerateArray()).GetString());
    }
}
