using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>Recorre el CRUD versionado de empresas con identidad, permisos y PostgreSQL reales.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class CompaniesTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Without_a_zone_the_company_takes_the_organization_zone()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = Tenant.CreateBusiness("Empresas zona heredada", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Cordoba", "ARS"), [account.UserId], account.UserId);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct);

        // Tablero Empresas, «Zona de negocio: La de la organización».
        using var created = await AccountJourney.PostAsync(client, "/api/companies", new
        {
            name = "Delta Norte S.A.",
            taxIdNumber = (string?)null,
            timeZoneId = (string?)null,
        }, Ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var companyId = await created.Content.ReadFromJsonAsync<Guid>(Ct);
        using var detail = await client.GetAsync($"/api/companies/{companyId:D}", Ct);
        using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(Ct));
        Assert.Equal("America/Argentina/Cordoba", json.RootElement.GetProperty("timeZoneId").GetString());
    }

    [Fact]
    public async Task Owner_creates_edits_filters_and_deactivates_a_company_with_version_checks()
    {
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        using var otherClient = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var otherAccount = await AccountJourney.RegisterAsync(factory, otherClient, Ct);
        var tenant = Tenant.CreateBusiness("Empresas HTTP test", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId], account.UserId);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        using var created = await AccountJourney.PostAsync(client, "/api/companies", new
        {
            name = "Sucursal Centro",
            taxIdNumber = (string?)null,
            timeZoneId = "America/Argentina/Buenos_Aires",
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var detailPath = created.Headers.Location!.IsAbsoluteUri
            ? created.Headers.Location.PathAndQuery
            : created.Headers.Location.OriginalString;
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        var companyId = createdJson.RootElement.GetGuid();

        using var detail = await client.GetAsync(detailPath, Ct);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Sucursal Centro", detailJson.RootElement.GetProperty("name").GetString());
        Assert.Equal(CompanyStatus.Active.ToString(), detailJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, detailJson.RootElement.GetProperty("memberCount").GetInt32());
        var createdVersion = detailJson.RootElement.GetProperty("version").GetUInt32();

        var companyMembersPath = $"/api/companies/{companyId:D}/members";
        using var initialMembers = await client.GetAsync(companyMembersPath + "/paged?page=1&pageSize=50", Ct);
        Assert.True(initialMembers.StatusCode == HttpStatusCode.OK,
            await initialMembers.Content.ReadAsStringAsync(Ct));
        using var initialMembersJson = JsonDocument.Parse(await initialMembers.Content.ReadAsStringAsync(Ct));
        var ownerMember = Assert.Single(initialMembersJson.RootElement.GetProperty("items").EnumerateArray());
        var ownerMemberId = ownerMember.GetProperty("memberId").GetGuid();
        var ownerMembershipVersion = ownerMember.GetProperty("version").GetUInt32();

        using var refusedLastAdminRemoval = await client.PutAsJsonAsync(
            $"{companyMembersPath}/{ownerMemberId:D}/status",
            new { status = CompanyMembershipStatus.Inactive.ToString(), version = ownerMembershipVersion }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, refusedLastAdminRemoval.StatusCode);
        using var lastAdminProblem = JsonDocument.Parse(await refusedLastAdminRemoval.Content.ReadAsStringAsync(Ct));
        Assert.Equal(CompanyMembershipErrors.LastCompanyAdminCode,
            lastAdminProblem.RootElement.GetProperty("code").GetString());
        // El texto del tablero de Empresa (lienzo v97), uno solo para todos los caminos.
        Assert.Equal("Tiene que quedar al menos un administrador en la empresa.",
            lastAdminProblem.RootElement.GetProperty("detail").GetString());

        Guid otherMemberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            var companyMember = Member.Invite(otherAccount.UserId);
            otherMemberId = companyMember.Id;
            Assert.True(companyMember.Activate(new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<IMemberRepository>().Add(companyMember);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        await AccountJourney.AuthorizeAsync(otherClient, "business", Ct);
        using var forbiddenWithoutCompanyMembership = await otherClient.GetAsync(
            companyMembersPath + "/paged?page=1&pageSize=10", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWithoutCompanyMembership.StatusCode);

        using var companyRolesResponse = await client.GetAsync("/api/roles/paged?scope=Company&page=1&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.OK, companyRolesResponse.StatusCode);
        using var companyRolesJson = JsonDocument.Parse(await companyRolesResponse.Content.ReadAsStringAsync(Ct));
        var companyAdmin = Assert.Single(companyRolesJson.RootElement.GetProperty("items").EnumerateArray(),
            role => role.GetProperty("isSystemRole").GetBoolean()
                && role.GetProperty("name").GetString() == "Administrador");
        var companyAdminRoleId = companyAdmin.GetProperty("id").GetGuid();
        using var addedMember = await AccountJourney.PostAsync(client, companyMembersPath, new
        {
            memberId = otherMemberId,
            roleIds = new[] { companyAdminRoleId },
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, addedMember.StatusCode);

        using var secondAdminMembers = await otherClient.GetAsync(companyMembersPath + "/paged?page=1&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.OK, secondAdminMembers.StatusCode);
        using var secondAdminMembersJson = JsonDocument.Parse(await secondAdminMembers.Content.ReadAsStringAsync(Ct));
        Assert.Equal(2, secondAdminMembersJson.RootElement.GetProperty("totalCount").GetInt32());
        var otherMember = Assert.Single(secondAdminMembersJson.RootElement.GetProperty("items").EnumerateArray(),
            member => member.GetProperty("memberId").GetGuid() == otherMemberId);
        Assert.Contains(otherMember.GetProperty("roles").EnumerateArray(), role =>
            role.GetProperty("name").GetString() == "Administrador");

        using var removedOwner = await client.PutAsJsonAsync(
            $"{companyMembersPath}/{ownerMemberId:D}/status",
            new { status = CompanyMembershipStatus.Inactive.ToString(), version = ownerMembershipVersion }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, removedOwner.StatusCode);

        using var updated = await client.PutAsJsonAsync(detailPath, new
        {
            name = "Sucursal Centro II",
            taxIdNumber = (string?)null,
            timeZoneId = "America/Argentina/Buenos_Aires",
            version = createdVersion,
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        using var staleUpdate = await client.PutAsJsonAsync(detailPath, new
        {
            name = "Versión obsoleta",
            taxIdNumber = (string?)null,
            timeZoneId = "America/Argentina/Buenos_Aires",
            version = createdVersion,
        }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        using var currentDetail = await client.GetAsync(detailPath, Ct);
        using var currentJson = JsonDocument.Parse(await currentDetail.Content.ReadAsStringAsync(Ct));
        var currentVersion = currentJson.RootElement.GetProperty("version").GetUInt32();
        using var inactive = await client.PutAsJsonAsync($"/api/companies/{companyId:D}/status", new
        {
            status = CompanyStatus.Inactive.ToString(),
            version = currentVersion,
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, inactive.StatusCode);

        using var list = await client.GetAsync("/api/companies/paged?status=Inactive&page=1&pageSize=10", Ct);
        Assert.True(list.StatusCode == HttpStatusCode.OK,
            await list.Content.ReadAsStringAsync(Ct));
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync(Ct));
        var listed = Assert.Single(listJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(companyId, listed.GetProperty("id").GetGuid());
        Assert.Equal(CompanyStatus.Inactive.ToString(), listed.GetProperty("status").GetString());

        using var inaccessibleAfterDeactivation = await otherClient.GetAsync(
            companyMembersPath + "/paged?page=1&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, inaccessibleAfterDeactivation.StatusCode);

        using var filterCounts = await client.GetAsync("/api/companies/filter-counts?search=Sucursal", Ct);
        Assert.Equal(HttpStatusCode.OK, filterCounts.StatusCode);
        using var countsJson = JsonDocument.Parse(await filterCounts.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, countsJson.RootElement.GetProperty("all").GetInt32());
        Assert.Equal(0, countsJson.RootElement.GetProperty("active").GetInt32());
        Assert.Equal(1, countsJson.RootElement.GetProperty("inactive").GetInt32());

        await AddCompaniesAsync(tenant.Id, 49);
        using var fifty = await client.GetAsync("/api/companies/paged?page=1&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.OK, fifty.StatusCode);
        using var fiftyJson = JsonDocument.Parse(await fifty.Content.ReadAsStringAsync(Ct));
        Assert.Equal(50, fiftyJson.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(50, fiftyJson.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Searching_company_members_by_text_finds_members_and_pending_invitations()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        await OrganizationJourney.StageAsync(factory, owner, "Búsqueda de miembros", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var companyId = await OrganizationJourney.CreateCompanyAsync(client, "Delta Norte S.A.", Ct);
        var membersPath = $"/api/companies/{companyId:D}/members/paged?pageSize=50&search=";
        var ownerText = owner.Email.Value[..owner.Email.Value.IndexOf('@', StringComparison.Ordinal)][^12..];

        // Sin invitaciones pendientes: busca por el correo del miembro.
        var found = await MembersAsync(client, membersPath + Uri.EscapeDataString(ownerText));
        Assert.Equal(owner.UserId, Assert.Single(found).GetProperty("userId").GetGuid());

        using var rolesResponse = await client.GetAsync($"/api/roles/paged?companyId={companyId:D}&pageSize=50", Ct);
        using var rolesJson = JsonDocument.Parse(await rolesResponse.Content.ReadAsStringAsync(Ct));
        var adminRoleId = Assert.Single(rolesJson.RootElement.GetProperty("items").EnumerateArray(),
            role => role.GetProperty("isSystemRole").GetBoolean()).GetProperty("id").GetGuid();
        var invited = $"pendiente-{Guid.NewGuid():N}@example.test";
        using var issued = await AccountJourney.PostAsync(client, "/api/users/invitations", new
        {
            email = invited,
            roleIds = Array.Empty<Guid>(),
            companies = new[] { new { companyId, roleIds = new[] { adminRoleId } } },
        }, Ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(Ct));

        // Con una invitación pendiente: la encuentra por su correo, y los comodines se buscan como texto.
        var pending = Assert.Single(await MembersAsync(client, membersPath + Uri.EscapeDataString(invited[..20])));
        Assert.Equal("Invited", pending.GetProperty("state").GetString());
        Assert.Equal(invited, pending.GetProperty("email").GetString());
        Assert.Equal(owner.UserId, Assert.Single(await MembersAsync(client, membersPath + Uri.EscapeDataString(ownerText)))
            .GetProperty("userId").GetGuid());
        Assert.Empty(await MembersAsync(client, membersPath + Uri.EscapeDataString("%_\\")));
    }

    [Theory]
    [InlineData("es-AR", HttpStatusCode.Created)]
    [InlineData("en-US", HttpStatusCode.BadRequest)]
    public async Task The_company_tax_id_takes_its_type_from_the_organization_country(string culture,
        HttpStatusCode expected)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = Tenant.CreateBusiness("Empresas con CUIT", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create(culture, "America/Argentina/Buenos_Aires", "ARS"), [owner.UserId], owner.UserId);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct);

        // D6: la ficha de Empresa pide solo el número.
        using var created = await AccountJourney.PostAsync(client, "/api/companies", new
        {
            name = "Delta Norte S.A.",
            taxIdNumber = "30-71234567-1",
            timeZoneId = (string?)null,
        }, Ct);

        Assert.True(created.StatusCode == expected, await created.Content.ReadAsStringAsync(Ct));
        if (expected != HttpStatusCode.Created)
        {
            using var problem = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
            Assert.Equal(TaxIdErrors.InvalidCode, problem.RootElement.GetProperty("code").GetString());
            return;
        }

        var companyId = await created.Content.ReadFromJsonAsync<Guid>(Ct);
        using var detail = await client.GetAsync($"/api/companies/{companyId:D}", Ct);
        using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(Ct));
        var taxId = json.RootElement.GetProperty("taxId");
        Assert.Equal("AR", taxId.GetProperty("country").GetString());
        Assert.Equal("AR-CUIT", taxId.GetProperty("type").GetString());
        Assert.Equal("30712345671", taxId.GetProperty("number").GetString());
    }

    // B4: un Administrador de empresa sin roles de la organización usa su empresa con sus permisos por empresa.
    [Fact]
    public async Task A_company_admin_without_organization_roles_gets_its_company_permissions_and_assignable_roles()
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Permisos por empresa", Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        var companyId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Delta Norte S.A.", Ct);
        var otherCompanyId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Delta Sur S.A.", Ct);
        await CreateRoleAsync(ownerClient, "Solo lectura Norte", "SpecificCompany", companyId);
        await CreateRoleAsync(ownerClient, "Solo lectura Sur", "SpecificCompany", otherCompanyId);
        await CreateRoleAsync(ownerClient, "Lectura en cada empresa", "AnyCompany", null);
        using var ownerRoles = await ownerClient.GetAsync("/api/roles/paged?scope=Company&page=1&pageSize=50", Ct);
        using var ownerRolesJson = JsonDocument.Parse(await ownerRoles.Content.ReadAsStringAsync(Ct));
        var companyAdminRoleId = ownerRolesJson.RootElement.GetProperty("items").EnumerateArray()
            .Single(role => role.GetProperty("isSystemRole").GetBoolean()).GetProperty("id").GetGuid();

        using var adminClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var admin = await AccountJourney.RegisterAsync(factory, adminClient, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, admin.UserId, Ct);
        using var added = await AccountJourney.PostAsync(ownerClient, $"/api/companies/{companyId:D}/members",
            new { memberId, roleIds = new[] { companyAdminRoleId } }, Ct);
        Assert.True(added.StatusCode == HttpStatusCode.NoContent, await added.Content.ReadAsStringAsync(Ct));
        await AccountJourney.AuthorizeAsync(adminClient, "business", Ct);

        // /api/me: ningún permiso de la organización y los de su empresa, solo para esa empresa.
        using var me = await adminClient.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meJson = JsonDocument.Parse(await me.Content.ReadAsStringAsync(Ct));
        var effective = meJson.RootElement.GetProperty("effectivePermissions");
        Assert.Empty(effective.GetProperty("organization").EnumerateArray());
        var company = Assert.Single(effective.GetProperty("companies").EnumerateObject());
        Assert.Equal(companyId, Guid.Parse(company.Name));
        Assert.Equal([Permissions.Company.Members.Manage, Permissions.Company.Members.Read],
            company.Value.EnumerateArray().Select(permission => permission.GetString()).Order(StringComparer.Ordinal));

        // Los roles que se pueden asignar en su empresa: los de cada empresa y los propios, sin los de otra empresa ni
        // los de toda la organización. La lista de roles de la organización sigue pidiendo roles.read.
        var names = await RoleNamesAsync(adminClient, $"/api/companies/{companyId:D}/roles/paged?page=1&pageSize=50");
        Assert.Equal(["Administrador", "Lectura en cada empresa", "Solo lectura Norte"], names.Order(StringComparer.Ordinal));
        Assert.Equal(["Solo lectura Norte"],
            await RoleNamesAsync(adminClient, $"/api/companies/{companyId:D}/roles/paged?search=norte"));
        using var organizationRoles = await adminClient.GetAsync("/api/roles/paged?page=1&pageSize=10", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, organizationRoles.StatusCode);
        using var otherCompany = await adminClient.GetAsync($"/api/companies/{otherCompanyId:D}/roles/paged", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, otherCompany.StatusCode);
        Assert.Equal(2, (await MembersAsync(adminClient, $"/api/companies/{companyId:D}/members/paged?pageSize=50")).Length);
    }

    // Alcance de un rol: Usuarios y Empresas pasan por la misma regla (RoleAssignment.Check) y responden el mismo
    // código para el mismo caso.
    [Fact]
    public async Task Users_and_companies_refuse_a_role_outside_its_scope_with_the_same_code()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        await OrganizationJourney.StageAsync(factory, owner, "Alcance de los roles", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var companyId = await OrganizationJourney.CreateCompanyAsync(client, "Delta Norte S.A.", Ct);
        var otherCompanyId = await OrganizationJourney.CreateCompanyAsync(client, "Delta Sur S.A.", Ct);
        await CreateRoleAsync(client, "Solo lectura Sur", "SpecificCompany", otherCompanyId);
        using var rolesResponse = await client.GetAsync("/api/roles/paged?page=1&pageSize=50", Ct);
        using var rolesJson = JsonDocument.Parse(await rolesResponse.Content.ReadAsStringAsync(Ct));
        var roles = rolesJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var ownerRoleId = roles.Single(role => role.GetProperty("isSystemRole").GetBoolean()
            && role.GetProperty("scope").GetString() == "Organization").GetProperty("id").GetGuid();
        var companyAdminRoleId = roles.Single(role => role.GetProperty("isSystemRole").GetBoolean()
            && role.GetProperty("scope").GetString() == "AnyCompany").GetProperty("id").GetGuid();
        var otherCompanyRoleId = roles.Single(role => role.GetProperty("name").GetString() == "Solo lectura Sur")
            .GetProperty("id").GetGuid();
        var ownerMemberId = Assert.Single(await MembersAsync(client, "/api/users/paged?pageSize=10"))
            .GetProperty("memberId").GetGuid();

        async Task AssertRefusedAsync(string path, object body, string code)
        {
            using var response = await AccountJourney.PostAsync(client, path, body, Ct);
            var content = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, content);
            using var problem = JsonDocument.Parse(content);
            Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
        }

        var membersPath = $"/api/companies/{companyId:D}/members";
        object Invitation(Guid[] organizationRoleIds, Guid[] companyRoleIds) => new
        {
            email = $"alcance-{Guid.NewGuid():N}@example.test",
            roleIds = organizationRoleIds,
            companies = companyRoleIds.Length == 0
                ? Array.Empty<object>()
                : new object[] { new { companyId, roleIds = companyRoleIds } },
        };

        // Un rol de toda la organización, en una empresa.
        await AssertRefusedAsync(membersPath, new { memberId = ownerMemberId, roleIds = new[] { ownerRoleId } },
            "Roles.RoleAssignment.CompanyNotAllowed");
        await AssertRefusedAsync("/api/users/invitations", Invitation([], [ownerRoleId]),
            "Roles.RoleAssignment.CompanyNotAllowed");
        // El rol propio de otra empresa.
        await AssertRefusedAsync(membersPath, new { memberId = ownerMemberId, roleIds = new[] { otherCompanyRoleId } },
            "Roles.RoleAssignment.CompanyMismatch");
        await AssertRefusedAsync("/api/users/invitations", Invitation([], [otherCompanyRoleId]),
            "Roles.RoleAssignment.CompanyMismatch");
        // Un rol de empresa entre los de la organización.
        await AssertRefusedAsync("/api/users/invitations", Invitation([companyAdminRoleId], []),
            "Roles.RoleAssignment.CompanyRequired");
    }

    private static async Task CreateRoleAsync(HttpClient client, string name, string scope, Guid? companyId)
    {
        using var created = await AccountJourney.PostAsync(client, "/api/roles", new
        {
            name,
            description = (string?)null,
            scope,
            companyId,
            permissions = new[] { Permissions.Company.Members.Read },
        }, Ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<string?[]> RoleNamesAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(items.Length, json.RootElement.GetProperty("totalCount").GetInt32());
        return [.. items.Select(role => role.GetProperty("name").GetString())];
    }

    private static async Task<JsonElement[]> MembersAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. json.RootElement.GetProperty("items").EnumerateArray().Select(item => item.Clone())];
    }

    private async Task AddCompaniesAsync(Guid tenantId, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            var repository = services.GetRequiredService<ICompanyRepository>();
            for (var index = 0; index < count; index++)
            {
                var company = Company.Create($"Empresa de prueba {index + 1:D2}", null,
                    "America/Argentina/Buenos_Aires");
                Assert.True(company.IsSuccess);
                repository.Add(company.Value);
            }

            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }
}
