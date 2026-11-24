using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Revisión adversarial de la B1 (ADR 0061): caminos que la guarda no cubre. Una invitación nueva da sus roles aunque la
/// persona invitada conserve los de una invitación revocada, y una empresa no dice nada de quien no está en ella.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class DelegatedAdministrationAdversarialTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Inviting_again_someone_whose_owner_invitation_was_revoked_asks_to_hold_the_owner()
    {
        var (owner, tenant) = await OrganizationAsync("Reinvitar");
        var ownerRole = await SystemRoleAsync(owner);
        var helpDeskRole = await CreateRoleAsync(owner, "Mesa", "Organization", null, [Permissions.Users.Invite]);
        var (helpDesk, _, _) = await MemberAsync(owner, tenant, [helpDeskRole]);

        // Una cuenta que ya existe: al revocar su invitación, su miembro queda invitado con los roles que traía.
        using var inviteeClient = Client();
        var invitee = await AccountJourney.RegisterAsync(factory, inviteeClient, Ct);
        using var issued = await AccountJourney.PostAsync(owner, "/api/users/invitations",
            new { email = invitee.Email.Value, roleIds = new[] { ownerRole }, companies = Array.Empty<object>() }, Ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(Ct));
        using var issuedJson = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(Ct));
        var invitationId = issuedJson.RootElement.GetProperty("invitationId").GetGuid();
        using (var revoked = await owner.DeleteAsync($"/api/users/invitations/{invitationId:D}", Ct))
            Assert.True(revoked.StatusCode == HttpStatusCode.NoContent, await revoked.Content.ReadAsStringAsync(Ct));

        // La Dueña la revocó. Quien solo invita no la vuelve a dar: una invitación nueva da el Dueño.
        using var reissued = await AccountJourney.PostAsync(helpDesk, "/api/users/invitations",
            new { email = invitee.Email.Value, roleIds = new[] { ownerRole }, companies = Array.Empty<object>() }, Ct);
        await AssertProblemAsync(reissued, HttpStatusCode.Forbidden, MemberErrors.RolesNotHeldCode);
    }

    [Fact]
    public async Task Inviting_again_someone_whose_company_invitation_was_revoked_asks_to_hold_the_company_role()
    {
        var (owner, tenant) = await OrganizationAsync("Reinvitar a Norte");
        var north = await OrganizationJourney.CreateCompanyAsync(owner, "Norte S.A.", Ct);
        var membersRole = await CreateRoleAsync(owner, "Gestión Norte", "SpecificCompany", north,
            [Permissions.Company.Members.Manage]);
        var helpDeskRole = await CreateRoleAsync(owner, "Mesa", "Organization", null, [Permissions.Users.Invite]);
        var (helpDesk, _, _) = await MemberAsync(owner, tenant, [helpDeskRole]);
        object[] companies = [new { companyId = north, roleIds = new[] { membersRole } }];

        using var inviteeClient = Client();
        var invitee = await AccountJourney.RegisterAsync(factory, inviteeClient, Ct);
        using var issued = await AccountJourney.PostAsync(owner, "/api/users/invitations",
            new { email = invitee.Email.Value, roleIds = Array.Empty<Guid>(), companies }, Ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(Ct));
        using var issuedJson = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(Ct));
        var invitationId = issuedJson.RootElement.GetProperty("invitationId").GetGuid();
        using (var revoked = await owner.DeleteAsync($"/api/users/invitations/{invitationId:D}", Ct))
            Assert.True(revoked.StatusCode == HttpStatusCode.NoContent, await revoked.Content.ReadAsStringAsync(Ct));

        // El rol de Norte que quedó guardado no vale todavía: la invitación nueva lo da, y quien invita no lo tiene.
        using var reissued = await AccountJourney.PostAsync(helpDesk, "/api/users/invitations",
            new { email = invitee.Email.Value, roleIds = Array.Empty<Guid>(), companies }, Ct);
        await AssertProblemAsync(reissued, HttpStatusCode.Forbidden, MemberErrors.RolesNotHeldCode);
    }

    [Fact]
    public async Task A_company_answers_not_found_for_someone_outside_it_whatever_their_permissions()
    {
        var (owner, tenant) = await OrganizationAsync("Fuera de la empresa");
        var north = await OrganizationJourney.CreateCompanyAsync(owner, "Norte S.A.", Ct);
        var membersRole = await CreateRoleAsync(owner, "Gestión Norte", "SpecificCompany", north,
            [Permissions.Company.Members.Manage]);
        var reading = await CreateRoleAsync(owner, "Lectura", "Organization", null, [Permissions.Users.Read]);
        var (manager, _, _) = await MemberAsync(owner, tenant, [], (north, [membersRole]));

        // Dos personas de la organización que no están en Norte: una sin roles y otra con un rol de organización.
        var plain = await OrganizationJourney.AddNamedMemberAsync(factory, tenant, "Sin roles", Ct);
        var reader = await OrganizationJourney.AddNamedMemberAsync(factory, tenant, "Lectora", Ct);
        using (var placed = await UpdateMemberAsync(owner, reader, [reading]))
            Assert.True(placed.IsSuccessStatusCode, await placed.Content.ReadAsStringAsync(Ct));

        // Quien administra Norte no ve a ninguna de las dos: las dos responden lo mismo, 404, y no si tiene más.
        foreach (var outsider in new[] { plain, reader })
        {
            using var roles = await manager.PutAsJsonAsync($"/api/companies/{north:D}/members/{outsider:D}/roles",
                new { roleIds = Array.Empty<Guid>(), version = 1u }, Ct);
            using var status = await manager.PutAsJsonAsync($"/api/companies/{north:D}/members/{outsider:D}/status",
                new { status = "Inactive", version = 1u }, Ct);
            await AssertProblemAsync(roles, HttpStatusCode.NotFound, CompanyMembershipErrors.NotFoundCode);
            await AssertProblemAsync(status, HttpStatusCode.NotFound, CompanyMembershipErrors.NotFoundCode);
        }
    }

    private HttpClient Client() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<(HttpClient Owner, Guid TenantId)> OrganizationAsync(string name)
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, account, name, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        return (client, tenantId);
    }

    private async Task<(HttpClient Client, Guid UserId, Guid MemberId)> MemberAsync(HttpClient owner, Guid tenantId,
        Guid[] organizationRoles, params (Guid CompanyId, Guid[] RoleIds)[] companies)
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, account.UserId, Ct);
        using (var assigned = await UpdateMemberAsync(owner, memberId, organizationRoles,
                   [.. companies.Select(company => (object)new { companyId = company.CompanyId, roleIds = company.RoleIds })]))
            Assert.True(assigned.IsSuccessStatusCode, await assigned.Content.ReadAsStringAsync(Ct));
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        return (client, account.UserId, memberId);
    }

    private static async Task<Guid> CreateRoleAsync(HttpClient client, string name, string scope, Guid? companyId,
        string[] permissions)
    {
        using var created = await AccountJourney.PostAsync(client, "/api/roles", new
        {
            name,
            description = (string?)null,
            scope,
            companyId,
            permissions,
        }, Ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        return await created.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    private static async Task<Guid> SystemRoleAsync(HttpClient owner) =>
        (await JsonAsync(owner, "/api/roles/paged?scope=Organization&pageSize=50")).GetProperty("items").EnumerateArray()
        .Single(role => role.GetProperty("isSystemRole").GetBoolean()).GetProperty("id").GetGuid();

    private static async Task<HttpResponseMessage> UpdateMemberAsync(HttpClient client, Guid memberId, Guid[] roleIds,
        params object[] companies) =>
        await client.PutAsJsonAsync($"/api/users/{memberId:D}", new
        {
            roleIds,
            companies,
            version = (await JsonAsync(client, $"/api/users/{memberId:D}")).GetProperty("version").GetUInt32(),
        }, Ct);

    private static async Task<JsonElement> JsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.Clone();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, $"{(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }
}
