using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Nadie da lo que no tiene (ADR 0061), con cuentas que tienen solo cada permiso, en los tres alcances: la organización,
/// una empresa y Plataforma. Quien administra roles, personas u operadores solo da o quita lo que tiene ahí, y solo
/// actúa sobre quien no tiene más; sobre uno mismo pasa, dar el Dueño pide todo el catálogo, y <c>grantable</c> de cada
/// listado dice lo mismo que la guarda (decisión 269).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class DelegatedAdministrationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task With_only_manage_roles_nobody_gives_or_takes_away_a_permission_they_do_not_have()
    {
        var (owner, tenant) = await OrganizationAsync("Solo roles");
        var managerRole = await CreateRoleAsync(owner, "Roles", "Organization", null, [Permissions.Roles.Manage]);
        var settingsRole = await CreateRoleAsync(owner, "Ajustes", "Organization", null, [Permissions.Settings.Manage]);
        var (manager, _, _) = await MemberAsync(owner, tenant, [managerRole]);

        using var foreign = await CreateRoleResponseAsync(manager, "Ajenos", [Permissions.Settings.Manage]);
        using var grown = await UpdateRoleAsync(manager, managerRole, "Roles", [Permissions.Roles.Manage, Permissions.Users.Manage]);
        using var takenAway = await UpdateRoleAsync(manager, settingsRole, "Ajustes", [Permissions.Roles.Read]);
        using var renamed = await UpdateRoleAsync(manager, settingsRole, "Ajustes generales", [Permissions.Settings.Manage]);
        using var own = await CreateRoleResponseAsync(manager, "Lectura de roles", [Permissions.Roles.Read]);

        await AssertProblemAsync(foreign, HttpStatusCode.Forbidden, RoleErrors.PermissionsNotHeldCode);
        await AssertProblemAsync(grown, HttpStatusCode.Forbidden, RoleErrors.PermissionsNotHeldCode);
        await AssertProblemAsync(takenAway, HttpStatusCode.Forbidden, RoleErrors.PermissionsNotHeldCode);
        Assert.True(renamed.StatusCode == HttpStatusCode.OK, await renamed.Content.ReadAsStringAsync(Ct));
        Assert.True(own.StatusCode == HttpStatusCode.Created, await own.Content.ReadAsStringAsync(Ct));
        Assert.Equal([Permissions.Roles.Manage, Permissions.Roles.Read],
            Strings((await JsonAsync(owner, $"/api/roles/{managerRole:D}")).GetProperty("permissions")));
        Assert.Equal([Permissions.Settings.Manage, Permissions.Settings.Read],
            Strings((await JsonAsync(owner, $"/api/roles/{settingsRole:D}")).GetProperty("permissions")));

        // grantable del catálogo: lo que tiene, con lo incluido.
        var catalog = (await JsonAsync(manager, "/api/permissions")).EnumerateArray()
            .SelectMany(group => group.GetProperty("permissions").EnumerateArray()).ToArray();
        Assert.Equal([Permissions.Roles.Read, Permissions.Roles.Manage], catalog
            .Where(item => item.GetProperty("grantable").GetBoolean()).Select(item => item.GetProperty("code").GetString()));
        Assert.All((await JsonAsync(owner, "/api/permissions")).EnumerateArray()
            .SelectMany(group => group.GetProperty("permissions").EnumerateArray()),
            item => Assert.True(item.GetProperty("grantable").GetBoolean()));
    }

    [Fact]
    public async Task With_only_manage_users_nobody_gives_a_role_with_more_or_acts_on_whoever_has_more()
    {
        var (owner, tenant) = await OrganizationAsync("Solo usuarios");
        var ownerRole = await SystemRoleAsync(owner, "Organization");
        var reading = await CreateRoleAsync(owner, "Lectura", "Organization", null, [Permissions.Users.Read]);
        await CreateRoleAsync(owner, "Ajustes", "Organization", null, [Permissions.Settings.Manage]);
        var helpDeskRole = await CreateRoleAsync(owner, "Mesa", "Organization", null,
            [Permissions.Users.Manage, Permissions.Users.Invite, Permissions.Roles.Read]);
        var (helpDesk, _, helpDeskMember) = await MemberAsync(owner, tenant, [helpDeskRole]);
        var other = await OrganizationJourney.AddNamedMemberAsync(factory, tenant, "Otra persona", Ct);
        var secondOwner = await OrganizationJourney.AddNamedMemberAsync(factory, tenant, "Otra Dueña", Ct);
        using (var promoted = await UpdateMemberAsync(owner, secondOwner, [ownerRole]))
            Assert.True(promoted.IsSuccessStatusCode, await promoted.Content.ReadAsStringAsync(Ct));

        // grantable de cada rol dice lo mismo que la guarda al darlo.
        var roles = (await JsonAsync(helpDesk, "/api/roles/paged?scope=Organization&pageSize=50"))
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(roles, role => role.GetProperty("grantable").GetBoolean());
        Assert.Contains(roles, role => !role.GetProperty("grantable").GetBoolean());
        Assert.False(roles.Single(role => role.GetProperty("id").GetGuid() == ownerRole).GetProperty("grantable").GetBoolean());
        foreach (var role in roles)
        {
            using var given = await UpdateMemberAsync(helpDesk, other, [role.GetProperty("id").GetGuid()]);
            if (role.GetProperty("grantable").GetBoolean())
                Assert.True(given.IsSuccessStatusCode, await given.Content.ReadAsStringAsync(Ct));
            else
                await AssertProblemAsync(given, HttpStatusCode.Forbidden, MemberErrors.RolesNotHeldCode);
        }

        // A sí misma no se da lo que no tiene, pero editarse sin cambios pasa: sobre uno mismo no se pide más.
        using var selfPromotion = await UpdateMemberAsync(helpDesk, helpDeskMember, [helpDeskRole, ownerRole]);
        using var self = await UpdateMemberAsync(helpDesk, helpDeskMember, [helpDeskRole]);
        await AssertProblemAsync(selfPromotion, HttpStatusCode.Forbidden, MemberErrors.RolesNotHeldCode);
        Assert.True(self.IsSuccessStatusCode, await self.Content.ReadAsStringAsync(Ct));

        // Invitar: solo con roles que puede dar.
        using var ownerInvitation = await AccountJourney.PostAsync(helpDesk, "/api/users/invitations",
            new { email = $"duena-{Guid.NewGuid():N}@example.test", roleIds = new[] { ownerRole }, companies = Array.Empty<object>() }, Ct);
        using var readerInvitation = await AccountJourney.PostAsync(helpDesk, "/api/users/invitations",
            new { email = $"lectora-{Guid.NewGuid():N}@example.test", roleIds = new[] { reading }, companies = Array.Empty<object>() }, Ct);
        await AssertProblemAsync(ownerInvitation, HttpStatusCode.Forbidden, MemberErrors.RolesNotHeldCode);
        Assert.True(readerInvitation.StatusCode == HttpStatusCode.Created, await readerInvitation.Content.ReadAsStringAsync(Ct));

        // Sobre quien tiene más: ni editarla, ni darla de baja, ni quitarla.
        using var edited = await UpdateMemberAsync(helpDesk, secondOwner, [ownerRole]);
        using var deactivated = await helpDesk.PutAsJsonAsync($"/api/users/{secondOwner:D}/status",
            new { isActive = false, version = await MemberVersionAsync(helpDesk, secondOwner) }, Ct);
        using var removed = await helpDesk.DeleteAsync(
            $"/api/users/{secondOwner:D}?version={await MemberVersionAsync(helpDesk, secondOwner)}", Ct);
        await AssertProblemAsync(edited, HttpStatusCode.Forbidden, MemberErrors.HasMorePermissionsCode);
        await AssertProblemAsync(deactivated, HttpStatusCode.Forbidden, MemberErrors.HasMorePermissionsCode);
        await AssertProblemAsync(removed, HttpStatusCode.Forbidden, MemberErrors.HasMorePermissionsCode);
        var detail = await JsonAsync(owner, $"/api/users/{secondOwner:D}");
        Assert.Equal("Active", detail.GetProperty("state").GetString());
        Assert.Equal([ownerRole], detail.GetProperty("roles").EnumerateArray().Select(role => role.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Giving_the_owner_asks_for_the_whole_catalog()
    {
        var (owner, tenant) = await OrganizationAsync("El Dueño");
        var ownerRole = await SystemRoleAsync(owner, "Organization");
        var almost = await CreateRoleAsync(owner, "Casi todo", "Organization", null,
            [.. Permissions.All.Where(code => code != Permissions.Audit.Read)]);
        var (almostClient, _, _) = await MemberAsync(owner, tenant, [almost]);
        var other = await OrganizationJourney.AddNamedMemberAsync(factory, tenant, "Futura Dueña", Ct);

        var listed = (await JsonAsync(almostClient, "/api/roles/paged?scope=Organization&pageSize=50"))
            .GetProperty("items").EnumerateArray().Single(role => role.GetProperty("id").GetGuid() == ownerRole);
        using var denied = await UpdateMemberAsync(almostClient, other, [ownerRole]);
        using var given = await UpdateMemberAsync(owner, other, [ownerRole]);
        using var actedOn = await UpdateMemberAsync(almostClient, other, []);

        Assert.False(listed.GetProperty("grantable").GetBoolean());
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, MemberErrors.RolesNotHeldCode);
        Assert.True(given.IsSuccessStatusCode, await given.Content.ReadAsStringAsync(Ct));
        await AssertProblemAsync(actedOn, HttpStatusCode.Forbidden, MemberErrors.HasMorePermissionsCode);
    }

    [Fact]
    public async Task In_a_company_only_manage_members_gives_what_it_has_there_and_never_acts_on_whoever_has_more()
    {
        var (owner, tenant) = await OrganizationAsync("Solo miembros");
        var north = await OrganizationJourney.CreateCompanyAsync(owner, "Norte S.A.", Ct);
        var companyAdmin = await SystemRoleAsync(owner, "Company");
        var reading = await CreateRoleAsync(owner, "Lectura", "Organization", null, [Permissions.Users.Read]);
        var membersRole = await CreateRoleAsync(owner, "Gestión Norte", "SpecificCompany", north,
            [Permissions.Company.Members.Manage]);
        var readers = await CreateRoleAsync(owner, "Lectores Norte", "SpecificCompany", north,
            [Permissions.Company.Members.Read]);
        var (manager, _, managerMember) = await MemberAsync(owner, tenant, [], (north, [membersRole]));
        var candidate = await OrganizationJourney.AddNamedMemberAsync(factory, tenant, "Candidata", Ct);
        var anotherCandidate = await OrganizationJourney.AddNamedMemberAsync(factory, tenant, "Otra candidata", Ct);
        var coAdmin = await OrganizationJourney.AddNamedMemberAsync(factory, tenant, "Coadministradora", Ct);
        using (var added = await AccountJourney.PostAsync(owner, $"/api/companies/{north:D}/members",
                   new { memberId = coAdmin, roleIds = new[] { companyAdmin } }, Ct))
            Assert.True(added.StatusCode == HttpStatusCode.NoContent, await added.Content.ReadAsStringAsync(Ct));
        var seller = await OrganizationJourney.AddNamedMemberAsync(factory, tenant, "Vendedora", Ct);
        using (var placed = await UpdateMemberAsync(owner, seller, [reading], new { companyId = north, roleIds = new[] { readers } }))
            Assert.True(placed.IsSuccessStatusCode, await placed.Content.ReadAsStringAsync(Ct));

        using var foreign = await AccountJourney.PostAsync(manager, $"/api/companies/{north:D}/members",
            new { memberId = anotherCandidate, roleIds = new[] { companyAdmin } }, Ct);
        using var allowed = await AccountJourney.PostAsync(manager, $"/api/companies/{north:D}/members",
            new { memberId = candidate, roleIds = new[] { readers } }, Ct);
        await AssertProblemAsync(foreign, HttpStatusCode.Forbidden, CompanyMembershipErrors.RolesNotHeldCode);
        Assert.True(allowed.StatusCode == HttpStatusCode.NoContent, await allowed.Content.ReadAsStringAsync(Ct));

        // grantable de los roles de la empresa dice lo mismo que la guarda al darlos.
        var roles = (await JsonAsync(manager, $"/api/companies/{north:D}/roles/paged?pageSize=50"))
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.False(roles.Single(role => role.GetProperty("id").GetGuid() == companyAdmin).GetProperty("grantable").GetBoolean());
        Assert.True(roles.Single(role => role.GetProperty("id").GetGuid() == readers).GetProperty("grantable").GetBoolean());
        foreach (var role in roles)
        {
            using var given = await CompanyRolesAsync(manager, north, candidate, [role.GetProperty("id").GetGuid()]);
            if (role.GetProperty("grantable").GetBoolean())
                Assert.True(given.IsSuccessStatusCode, await given.Content.ReadAsStringAsync(Ct));
            else
                await AssertProblemAsync(given, HttpStatusCode.Forbidden, CompanyMembershipErrors.RolesNotHeldCode);
        }

        // Sobre quien tiene más en la empresa, también por sus roles de organización: ni roles ni estado.
        using var coAdminRoles = await CompanyRolesAsync(manager, north, coAdmin, [readers]);
        using var coAdminStatus = await CompanyStatusAsync(manager, north, coAdmin, "Inactive");
        using var sellerStatus = await CompanyStatusAsync(manager, north, seller, "Inactive");
        await AssertProblemAsync(coAdminRoles, HttpStatusCode.Forbidden, CompanyMembershipErrors.HasMorePermissionsCode);
        await AssertProblemAsync(coAdminStatus, HttpStatusCode.Forbidden, CompanyMembershipErrors.HasMorePermissionsCode);
        await AssertProblemAsync(sellerStatus, HttpStatusCode.Forbidden, CompanyMembershipErrors.HasMorePermissionsCode);

        // Sobre sí misma pasa, con lo que tiene ahí.
        using var self = await CompanyRolesAsync(manager, north, managerMember, [membersRole, readers]);
        Assert.True(self.IsSuccessStatusCode, await self.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Between_operators_nobody_gives_what_they_do_not_have_nor_acts_on_whoever_has_more()
    {
        var (manager, _) = await OperatorAsync([PlatformPermissions.Operators.Manage, PlatformPermissions.Accounts.Manage]);

        // grantable del catálogo dice lo mismo que la guarda al invitar, permiso por permiso.
        var catalog = (await JsonAsync(manager, "/api/platform/permissions")).EnumerateArray().ToArray();
        Assert.Equal(
            [PlatformPermissions.Accounts.Read, PlatformPermissions.Accounts.Manage, PlatformPermissions.Operators.Manage],
            catalog.Where(item => item.GetProperty("grantable").GetBoolean()).Select(item => item.GetProperty("code").GetString()));
        foreach (var item in catalog)
        {
            using var invited = await PlatformJourney.PostAsync(manager, "/api/platform/operators/invitations", new
            {
                email = $"operador-{Guid.NewGuid():N}@example.test",
                permissions = new[] { item.GetProperty("code").GetString() },
            }, Ct);
            if (item.GetProperty("grantable").GetBoolean())
                Assert.True(invited.StatusCode == HttpStatusCode.Created, await invited.Content.ReadAsStringAsync(Ct));
            else
                await AssertProblemAsync(invited, HttpStatusCode.Forbidden, PlatformOperatorErrors.PermissionsNotHeldCode);
        }

        // Cambiar los permisos: solo los que tiene, también al sacarlos.
        var (_, lesser) = await OperatorAsync([PlatformPermissions.Accounts.Read, PlatformPermissions.Audit.Read]);
        using var foreign = await ChangeOperatorAsync(manager, lesser,
            [PlatformPermissions.Accounts.Read, PlatformPermissions.Audit.Read, PlatformPermissions.Tenants.Read]);
        using var takenAway = await ChangeOperatorAsync(manager, lesser, [PlatformPermissions.Accounts.Read]);
        using var kept = await ChangeOperatorAsync(manager, lesser, [PlatformPermissions.Accounts.Manage, PlatformPermissions.Audit.Read]);
        await AssertProblemAsync(foreign, HttpStatusCode.Forbidden, PlatformOperatorErrors.PermissionsNotHeldCode);
        await AssertProblemAsync(takenAway, HttpStatusCode.Forbidden, PlatformOperatorErrors.PermissionsNotHeldCode);
        Assert.True(kept.StatusCode == HttpStatusCode.OK, await kept.Content.ReadAsStringAsync(Ct));

        // Sobre un operador que tiene más: ni suspenderlo, ni reactivarlo, ni darlo de baja, ni quitarlo.
        var (_, stronger) = await OperatorAsync(PlatformOperators.All);
        var path = $"/api/platform/accounts/{stronger:D}";
        using var suspended = await PlatformJourney.PostAsync(manager, path + "/suspend", new { reason = "Prueba" }, Ct);
        using var reactivated = await PlatformJourney.PostAsync(manager, path + "/reactivate", new { reason = "Prueba" }, Ct);
        using var deletion = await PlatformJourney.PostAsync(manager, path + "/deletion", new { reason = "Prueba" }, Ct);
        using var removed = await SendAsync(manager, HttpMethod.Delete, path + "/operator",
            new { version = await OperatorVersionAsync(manager, stronger), reason = "Prueba" });
        await AssertProblemAsync(suspended, HttpStatusCode.Forbidden, PlatformOperatorErrors.HasMorePermissionsCode);
        await AssertProblemAsync(reactivated, HttpStatusCode.Forbidden, PlatformOperatorErrors.HasMorePermissionsCode);
        await AssertProblemAsync(deletion, HttpStatusCode.Forbidden, PlatformOperatorErrors.HasMorePermissionsCode);
        await AssertProblemAsync(removed, HttpStatusCode.Forbidden, PlatformOperatorErrors.HasMorePermissionsCode);
        var detail = await JsonAsync(manager, path);
        Assert.Equal("Active", detail.GetProperty("state").GetString());
        Assert.Equal(PlatformPermissions.All.Count, detail.GetProperty("operatorPermissions").GetArrayLength());

        // Sobre uno que no tiene más, sí; y el que conservó «Ver auditoría», que ella no tiene, tampoco.
        var (_, peer) = await OperatorAsync([PlatformPermissions.Accounts.Read]);
        using var peerSuspended = await PlatformJourney.PostAsync(manager, $"/api/platform/accounts/{peer:D}/suspend",
            new { reason = "Prueba" }, Ct);
        using var lesserSuspended = await PlatformJourney.PostAsync(manager, $"/api/platform/accounts/{lesser:D}/suspend",
            new { reason = "Prueba" }, Ct);
        Assert.True(peerSuspended.StatusCode == HttpStatusCode.NoContent, await peerSuspended.Content.ReadAsStringAsync(Ct));
        await AssertProblemAsync(lesserSuspended, HttpStatusCode.Forbidden, PlatformOperatorErrors.HasMorePermissionsCode);
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

    /// <summary>Una cuenta nueva, miembro activo con esos roles (dados por el Dueño), con su sesión en la organización.</summary>
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

    private async Task<(HttpClient Client, Guid Id)> OperatorAsync(IReadOnlyCollection<string> permissions)
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return (client, account.UserId);
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

    private static Task<HttpResponseMessage> CreateRoleResponseAsync(HttpClient client, string name, string[] permissions) =>
        AccountJourney.PostAsync(client, "/api/roles", new
        {
            name,
            description = (string?)null,
            scope = "Organization",
            companyId = (Guid?)null,
            permissions,
        }, Ct);

    private static async Task<HttpResponseMessage> UpdateRoleAsync(HttpClient client, Guid roleId, string name,
        string[] permissions)
    {
        var version = (await JsonAsync(client, $"/api/roles/{roleId:D}")).GetProperty("version").GetUInt32();
        return await client.PutAsJsonAsync($"/api/roles/{roleId:D}", new
        {
            name,
            description = (string?)null,
            scope = "Organization",
            companyId = (Guid?)null,
            permissions,
            version,
        }, Ct);
    }

    // El rol de sistema del alcance: el Dueño en la organización, el Administrador de empresa en las empresas.
    private static async Task<Guid> SystemRoleAsync(HttpClient owner, string scope) =>
        (await JsonAsync(owner, $"/api/roles/paged?scope={scope}&pageSize=50")).GetProperty("items").EnumerateArray()
        .Single(role => role.GetProperty("isSystemRole").GetBoolean()).GetProperty("id").GetGuid();

    private static async Task<HttpResponseMessage> UpdateMemberAsync(HttpClient client, Guid memberId, Guid[] roleIds,
        params object[] companies) =>
        await client.PutAsJsonAsync($"/api/users/{memberId:D}", new
        {
            roleIds,
            companies,
            version = await MemberVersionAsync(client, memberId),
        }, Ct);

    private static async Task<uint> MemberVersionAsync(HttpClient client, Guid memberId) =>
        (await JsonAsync(client, $"/api/users/{memberId:D}")).GetProperty("version").GetUInt32();

    private static async Task<HttpResponseMessage> CompanyRolesAsync(HttpClient client, Guid companyId, Guid memberId,
        Guid[] roleIds) =>
        await client.PutAsJsonAsync($"/api/companies/{companyId:D}/members/{memberId:D}/roles",
            new { roleIds, version = await MembershipVersionAsync(client, companyId, memberId) }, Ct);

    private static async Task<HttpResponseMessage> CompanyStatusAsync(HttpClient client, Guid companyId, Guid memberId,
        string status) =>
        await client.PutAsJsonAsync($"/api/companies/{companyId:D}/members/{memberId:D}/status",
            new { status, version = await MembershipVersionAsync(client, companyId, memberId) }, Ct);

    private static async Task<uint> MembershipVersionAsync(HttpClient client, Guid companyId, Guid memberId) =>
        (await JsonAsync(client, $"/api/companies/{companyId:D}/members/paged?pageSize=50")).GetProperty("items")
        .EnumerateArray().Single(item => item.GetProperty("memberId").GetGuid() == memberId)
        .GetProperty("version").GetUInt32();

    private static async Task<HttpResponseMessage> ChangeOperatorAsync(HttpClient client, Guid accountId,
        string[] permissions) =>
        await SendAsync(client, HttpMethod.Put, $"/api/platform/accounts/{accountId:D}/operator", new
        {
            version = await OperatorVersionAsync(client, accountId),
            reason = "Prueba",
            permissions,
        });

    private static async Task<uint> OperatorVersionAsync(HttpClient client, Guid accountId) =>
        (await JsonAsync(client, $"/api/platform/accounts/{accountId:D}")).GetProperty("operatorVersion").GetUInt32();

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return client.SendAsync(request, Ct);
    }

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

    private static string?[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString())];
}
