using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        Assert.True(issued.StatusCode == HttpStatusCode.Created,
            await issued.Content.ReadAsStringAsync(Ct));
        // Tablero Usuarios: «Invitamos a … La invitación vence el …».
        using var issuedJson = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(Ct));
        var invitationId = issuedJson.RootElement.GetProperty("invitationId").GetGuid();
        var expiresAtUtc = issuedJson.RootElement.GetProperty("expiresAtUtc").GetDateTime();

        using var listed = await ownerClient.GetAsync("/api/users/paged?page=1&pageSize=50&state=Invited", Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listJson = JsonDocument.Parse(await listed.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, listJson.RootElement.GetProperty("totalCount").GetInt32());
        var invited = Assert.Single(listJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(recipient.DisplayValue, invited.GetProperty("email").GetString());
        var invitedMemberId = invited.GetProperty("memberId").GetGuid();

        using var resent = await AccountJourney.PostAsync(ownerClient,
            $"/api/users/invitations/{invitationId:D}/resend", new { }, Ct);
        Assert.True(resent.StatusCode == HttpStatusCode.OK,
            await resent.Content.ReadAsStringAsync(Ct));
        var resentInvitationId = await resent.Content.ReadFromJsonAsync<Guid>(Ct);
        Assert.NotEqual(invitationId, resentInvitationId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var invitations = scope.ServiceProvider.GetRequiredService<IInvitationReader>();
            var first = await invitations.FindByIdAsync(invitationId, Ct);
            Assert.Equal(InvitationStatus.Revoked, first!.Status);
            Assert.Equal(first.ExpiresAtUtc, expiresAtUtc.ToUniversalTime(), TimeSpan.FromMilliseconds(1));
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
        Assert.Equal("Tenancy.Member.LastTenantAdmin", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("Tiene que quedar al menos un Dueño activo.", problem.RootElement.GetProperty("detail").GetString());

        using var selfDisabled = await client.PutAsJsonAsync($"/api/users/{ownerMemberId:D}/status",
            new { isActive = false, version }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, selfDisabled.StatusCode);
        using var selfProblem = JsonDocument.Parse(await selfDisabled.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Tenancy.Member.CannotDisableSelf", selfProblem.RootElement.GetProperty("code").GetString());

        using var rolesRemoved = await client.PutAsJsonAsync($"/api/users/{ownerMemberId:D}",
            new { roleIds = Array.Empty<Guid>(), companies = Array.Empty<object>(), version }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, rolesRemoved.StatusCode);
        using var rolesProblem = JsonDocument.Parse(await rolesRemoved.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Tenancy.Member.LastTenantAdmin", rolesProblem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Updating_organization_roles_preserves_company_roles_for_an_inactive_membership()
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        using var memberClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false });
        var user = await AccountJourney.RegisterAsync(factory, memberClient, Ct);
        var (tenantId, _) = await CreateOrganizationAsync(owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);

        using var createdCompany = await AccountJourney.PostAsync(ownerClient, "/api/companies", new
        {
            name = "Sucursal inactiva",
            taxIdNumber = (string?)null,
            timeZoneId = "America/Argentina/Buenos_Aires",
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, createdCompany.StatusCode);
        var companyId = await createdCompany.Content.ReadFromJsonAsync<Guid>(Ct);

        Guid memberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var member = Member.Invite(user.UserId);
            memberId = member.Id;
            Assert.True(member.Activate(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<IMemberRepository>().Add(member);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        using var roleResponse = await ownerClient.GetAsync("/api/roles/paged?scope=Company&page=1&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);
        using var rolesJson = JsonDocument.Parse(await roleResponse.Content.ReadAsStringAsync(Ct));
        var companyAdmin = Assert.Single(rolesJson.RootElement.GetProperty("items").EnumerateArray(),
            role => role.GetProperty("isSystemRole").GetBoolean() && role.GetProperty("name").GetString() == "Administrador");
        var companyAdminRoleId = companyAdmin.GetProperty("id").GetGuid();
        var companyMembersPath = $"/api/companies/{companyId:D}/members";
        using var added = await AccountJourney.PostAsync(ownerClient, companyMembersPath, new
        {
            memberId,
            roleIds = new[] { companyAdminRoleId },
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);

        using var membersResponse = await ownerClient.GetAsync(companyMembersPath + "/paged?page=1&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.OK, membersResponse.StatusCode);
        using var membersJson = JsonDocument.Parse(await membersResponse.Content.ReadAsStringAsync(Ct));
        var companyMember = Assert.Single(membersJson.RootElement.GetProperty("items").EnumerateArray(),
            row => row.GetProperty("memberId").GetGuid() == memberId);
        var companyMembershipVersion = companyMember.GetProperty("version").GetUInt32();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var membership = await services.GetRequiredService<ICompanyMembershipRepository>()
                    .GetByIdAsync(companyId, memberId, companyMembershipVersion, ct);
                Assert.NotNull(membership);
                membership.SetStatus(CompanyMembershipStatus.Inactive);
                services.GetRequiredService<ICompanyMembershipRepository>().MarkUpdated(membership);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        using var userDetailResponse = await ownerClient.GetAsync($"/api/users/{memberId:D}", Ct);
        Assert.Equal(HttpStatusCode.OK, userDetailResponse.StatusCode);
        using var userDetailJson = JsonDocument.Parse(await userDetailResponse.Content.ReadAsStringAsync(Ct));
        var userVersion = userDetailJson.RootElement.GetProperty("version").GetUInt32();
        using var updated = await ownerClient.PutAsJsonAsync($"/api/users/{memberId:D}", new
        {
            roleIds = Array.Empty<Guid>(),
            companies = Array.Empty<object>(),
            version = userVersion,
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        using var afterResponse = await ownerClient.GetAsync($"/api/users/{memberId:D}", Ct);
        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);
        using var afterJson = JsonDocument.Parse(await afterResponse.Content.ReadAsStringAsync(Ct));
        var company = Assert.Single(afterJson.RootElement.GetProperty("companies").EnumerateArray());
        Assert.Equal(CompanyMembershipStatus.Inactive.ToString(), company.GetProperty("status").GetString());
        Assert.Equal(companyAdminRoleId, Assert.Single(company.GetProperty("roles").EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Saving_the_user_changes_status_with_roles_and_shows_when_the_account_is_deleted()
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        using var memberClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false });
        var user = await AccountJourney.RegisterAsync(factory, memberClient, Ct);
        var (tenantId, ownerMemberId) = await CreateOrganizationAsync(owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        var memberId = await AddActiveMemberAsync(tenantId, user.UserId);

        // Tablero Usuario: «Guardar cambios» manda estado, roles y empresas juntos.
        using var disabled = await ownerClient.PutAsJsonAsync($"/api/users/{memberId:D}", new
        {
            isActive = false,
            roleIds = Array.Empty<Guid>(),
            companies = Array.Empty<object>(),
            version = await UserVersionAsync(ownerClient, memberId),
        }, Ct);
        Assert.True(disabled.StatusCode == HttpStatusCode.NoContent, await disabled.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Inactive", await UserStateAsync(ownerClient, memberId));

        using var enabled = await ownerClient.PutAsJsonAsync($"/api/users/{memberId:D}", new
        {
            isActive = true,
            roleIds = Array.Empty<Guid>(),
            companies = Array.Empty<object>(),
            version = await UserVersionAsync(ownerClient, memberId),
        }, Ct);
        Assert.True(enabled.StatusCode == HttpStatusCode.NoContent, await enabled.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Active", await UserStateAsync(ownerClient, memberId));

        using var selfDisabled = await ownerClient.PutAsJsonAsync($"/api/users/{ownerMemberId:D}", new
        {
            isActive = false,
            roleIds = Array.Empty<Guid>(),
            companies = Array.Empty<object>(),
            version = await UserVersionAsync(ownerClient, ownerMemberId),
        }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, selfDisabled.StatusCode);
        using var selfProblem = JsonDocument.Parse(await selfDisabled.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Tenancy.Member.CannotDisableSelf", selfProblem.RootElement.GetProperty("code").GetString());

        DateTime requestedAtUtc;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            requestedAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var requested = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
                services.GetRequiredService<IUserRepository>().RequestDeletionAsync(user.UserId, "Prueba",
                    requestedAtUtc, 30, ct), CommitPolicy.OnSuccess, Ct);
            Assert.True(requested.IsSuccess);
        }
        using var pending = await ownerClient.GetAsync($"/api/users/{memberId:D}", Ct);
        using var pendingJson = JsonDocument.Parse(await pending.Content.ReadAsStringAsync(Ct));
        Assert.Equal("PendingDeletion", pendingJson.RootElement.GetProperty("state").GetString());
        // «Pidió la baja de su cuenta. Se elimina el …»
        Assert.Equal(requestedAtUtc.AddDays(30), pendingJson.RootElement.GetProperty("deletionScheduledForUtc")
            .GetDateTime(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task The_invitation_shows_who_invited_with_their_role_and_the_company_and_roles_offered()
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        await CreateOrganizationAsync(owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        using var createdCompany = await AccountJourney.PostAsync(ownerClient, "/api/companies", new
        {
            name = "Delta Norte S.A.",
            taxIdNumber = (string?)null,
            timeZoneId = "America/Argentina/Buenos_Aires",
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, createdCompany.StatusCode);
        var companyId = await createdCompany.Content.ReadFromJsonAsync<Guid>(Ct);
        using var roleResponse = await ownerClient.GetAsync("/api/roles/paged?scope=Company&page=1&pageSize=50", Ct);
        using var rolesJson = JsonDocument.Parse(await roleResponse.Content.ReadAsStringAsync(Ct));
        var companyAdminRoleId = Assert.Single(rolesJson.RootElement.GetProperty("items").EnumerateArray(),
            role => role.GetProperty("isSystemRole").GetBoolean() && role.GetProperty("name").GetString() == "Administrador")
            .GetProperty("id").GetGuid();

        var recipient = Email.Create($"invite-{Guid.NewGuid():N}@example.test").Value;
        using var issued = await AccountJourney.PostAsync(ownerClient, "/api/users/invitations", new
        {
            email = recipient.Value,
            roleIds = Array.Empty<Guid>(),
            companies = new[] { new { companyId, roleIds = new[] { companyAdminRoleId } } },
        }, Ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(Ct));

        // Tablero Mensajes: el correo lista Empresa y Roles; tablero Invitacion: «Lucía Fernández te invitó · Dueño».
        var email = await InvitationEmailAsync(recipient);
        Assert.Contains("Empresa: Delta Norte S.A.", email.TextBody);
        Assert.Contains("Roles: Administrador", email.TextBody);
        var token = Regex.Match(email.TextBody, @"/invitacion#(?<token>[A-Za-z0-9_-]+)").Groups["token"].Value;
        var preview = await PreviewInvitationAsync(token);
        Assert.True(preview.IsSuccess);
        Assert.Equal(InvitationPreviewState.NoAccount, preview.Value.State);
        Assert.Equal(["Dueño"], preview.Value.InviterRoles);
        Assert.Equal(["Delta Norte S.A."], preview.Value.Companies);
        Assert.Equal(["Administrador"], preview.Value.Roles);
    }

    private async Task<EmailMessage> InvitationEmailAsync(Email recipient)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var protector = services.GetRequiredService<IPayloadProtector>();
        var payloads = await services.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(row => row.Channel == "email" && row.EncryptedPayload != string.Empty)
            .Select(row => row.EncryptedPayload).ToListAsync(Ct);
        var messages = new List<EmailMessage>();
        foreach (var payload in payloads)
        {
            string cleartext;
            try { cleartext = protector.Unprotect(payload); }
            catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException
                or FormatException)
            {
                // Other tests in this fixture deliberately enqueue invalid payloads.
                continue;
            }
            if (JsonSerializer.Deserialize<EmailMessage>(cleartext) is { } message)
                messages.Add(message);
        }
        return Assert.Single(messages, message => message.To == recipient.Value);
    }

    private async Task<Result<InvitationPreviewResponse>> PreviewInvitationAsync(string token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { RequestServices = services, Request = { Scheme = "https" } };
        try
        {
            return await services.GetRequiredService<IInvitationService>()
                .PreviewAsync(new PreviewInvitationRequest(token), Ct);
        }
        finally { accessor.HttpContext = null; }
    }

    private static async Task<uint> UserVersionAsync(HttpClient client, Guid memberId)
    {
        using var response = await client.GetAsync($"/api/users/{memberId:D}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("version").GetUInt32();
    }

    private static async Task<string?> UserStateAsync(HttpClient client, Guid memberId)
    {
        using var response = await client.GetAsync($"/api/users/{memberId:D}", Ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("state").GetString();
    }

    private async Task<Guid> AddActiveMemberAsync(Guid tenantId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var member = Member.Invite(userId);
        Assert.True(member.Activate(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<IMemberRepository>().Add(member);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return member.Id;
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
