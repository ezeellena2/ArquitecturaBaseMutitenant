using ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>ADR 0040: cada fuente de «Para resolver» lee con el tenant del claim; lo de otra organización nunca aparece.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class HomeIsolationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_pending_item_of_another_organization_never_appears()
    {
        using var otherClient = NewClient();
        var otherOwner = await AccountJourney.RegisterAsync(factory, otherClient, Ct);
        var otherTenantId = await OrganizationJourney.StageAsync(factory, otherOwner, "Inicio ajeno", Ct);
        await AccountJourney.AuthorizeAsync(otherClient, "business", Ct);
        var otherInvitee = $"ajeno-{Guid.NewGuid():N}@example.test";
        await OrganizationJourney.InviteExpiredAsync(factory, otherClient, otherTenantId, otherInvitee, Ct);
        var otherDomain = $"ajeno-{Guid.NewGuid():N}.example";
        await OrganizationJourney.RegisterDomainAsync(otherClient, otherDomain, Ct);

        using var client = NewClient();
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        await OrganizationJourney.StageAsync(factory, owner, "Inicio propio", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);

        var home = await OrganizationJourney.HomeAsync(client, Ct);

        Assert.Equal([("publicPage", "publicPageDraft", null)], HomeTests.Pending(home));
        Assert.Equal(1, home.GetProperty("organization").GetProperty("users").GetProperty("total").GetInt32());
        Assert.DoesNotContain(otherInvitee, home.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(otherDomain, home.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var otherHome = await OrganizationJourney.HomeAsync(otherClient, Ct);
        Assert.Contains(("users", "invitationExpired", otherInvitee), HomeTests.Pending(otherHome));
        Assert.Contains(("settings", "domainUnverified", otherDomain), HomeTests.Pending(otherHome));
    }

    private HttpClient NewClient() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
}
