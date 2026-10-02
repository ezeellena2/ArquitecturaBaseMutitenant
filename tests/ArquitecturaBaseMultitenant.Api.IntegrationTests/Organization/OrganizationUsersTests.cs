using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>Recorre Usuarios e Invitaciones sobre PostgreSQL aislado con identidades y roles reales.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class OrganizationUsersTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_invites_unknown_email_resends_and_revokes_each_invitation_independently()
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var (tenantId, ownerMemberId) = await CreateOrganizationAsync(owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);

        var recipient = Email.Create($"invite-{Guid.NewGuid():N}@example.test").Value;
        using var issued = await AccountJourney.PostAsync(ownerClient, "/api/users/invitations", new
        {
            email = recipient.Value,
            roleIds = Array.Empty<Guid>(),
            companies = Array.Empty<object>(),
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        var invitationId = await issued.Content.ReadFromJsonAsync<Guid>(Ct);

        using var listed = await ownerClient.GetAsync("/api/users/paged?page=1&pageSize=50&state=Invited", Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listJson = JsonDocument.Parse(await listed.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, listJson.RootElement.GetProperty("totalCount").GetInt32());
        var invited = Assert.Single(listJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(recipient.DisplayValue, invited.GetProperty("email").GetString());
        var invitedMemberId = invited.GetProperty("memberId").GetGuid();

        using var resent = await AccountJourney.PostAsync(ownerClient,
            $"/api/users/invitations/{invitationId:D}/resend", new { }, Ct);
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        var resentInvitationId = await resent.Content.ReadFromJsonAsync<Guid>(Ct);
        Assert.NotEqual(invitationId, resentInvitationId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var invitations = scope.ServiceProvider.GetRequiredService<IInvitationReader>();
            Assert.Equal(InvitationStatus.Revoked,
                (await invitations.FindByIdAsync(invitationId, Ct))!.Status);
            var current = await invitations.FindByIdAsync(resentInvitationId, Ct);
            Assert.Equal(InvitationStatus.Pending, current!.Status);
            Assert.Equal(invitedMemberId, current.MemberId);
        }

        using var revoked = await ownerClient.DeleteAsync($"/api/users/invitations/{resentInvitationId:D}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var current = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var invitation = await services.GetRequiredService<IInvitationReader>()
                    .FindByIdAsync(resentInvitationId, ct);
                var member = await services.GetRequiredService<IMemberRepository>()
                    .GetByIdAsync(invitedMemberId, ct);
                return Result.Success((invitation!.Status, member!.Status));
            }, CommitPolicy.OnSuccess, Ct);
            Assert.Equal(InvitationStatus.Revoked, current.Value.Item1);
            Assert.Equal(MemberStatus.Removed, current.Value.Item2);
        }
        using var afterRevoke = await ownerClient.GetAsync("/api/users/paged?page=1&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.OK, afterRevoke.StatusCode);
        using var afterRevokeJson = JsonDocument.Parse(await afterRevoke.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, afterRevokeJson.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(ownerMemberId, Assert.Single(afterRevokeJson.RootElement.GetProperty("items").EnumerateArray())
            .GetProperty("memberId").GetGuid());
    }

    [Fact]
    public async Task Owner_lists_fifty_members_and_cannot_remove_the_last_dueño()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var (tenantId, ownerMemberId) = await CreateOrganizationAsync(owner.UserId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        await AddInvitationsAsync(tenantId, owner.UserId, 49);

        using var list = await client.GetAsync("/api/users/paged?page=1&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync(Ct));
        Assert.Equal(50, listJson.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(50, listJson.RootElement.GetProperty("totalCount").GetInt32());
        using var counts = await client.GetAsync("/api/users/filter-counts", Ct);
        using var countsJson = JsonDocument.Parse(await counts.Content.ReadAsStringAsync(Ct));
        Assert.Equal(50, countsJson.RootElement.GetProperty("all").GetInt32());
        Assert.Equal(49, countsJson.RootElement.GetProperty("invited").GetInt32());
        Assert.Equal(1, countsJson.RootElement.GetProperty("active").GetInt32());

        var ownerRow = Assert.Single(listJson.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("memberId").GetGuid() == ownerMemberId);
        var version = ownerRow.GetProperty("version").GetUInt32();
        using var refused = await client.DeleteAsync($"/api/users/{ownerMemberId:D}?version={version}", Ct);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Legal.AccountDeletion.LastAdmin", problem.RootElement.GetProperty("code").GetString());
    }

    private async Task<(Guid TenantId, Guid MemberId)> CreateOrganizationAsync(Guid ownerId)
    {
        var tenant = Tenant.CreateBusiness("Usuarios HTTP test", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [ownerId], ownerId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        var members = await services.GetRequiredService<IOrganizationUserReader>().ListAsync(
            new ListOrganizationUsersRequest { PageSize = 10 },
            services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, Ct);
        var ownerRow = Assert.Single(members.Items, row => row.UserId == ownerId);
        return (tenant.Id, ownerRow.MemberId);
    }

    private async Task AddInvitationsAsync(Guid tenantId, Guid inviterId, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        for (var index = 0; index < count; index++)
        {
            var destination = Email.Create($"page-{Guid.NewGuid():N}@example.test").Value;
            var issued = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
                services.GetRequiredService<InvitationIssuer>().IssueAsync(
                    new IssueInvitationRequest(destination, inviterId, InvitationChannel.Email), ct),
                CommitPolicy.OnSuccess, Ct);
            Assert.True(issued.IsSuccess);
        }
    }
}
