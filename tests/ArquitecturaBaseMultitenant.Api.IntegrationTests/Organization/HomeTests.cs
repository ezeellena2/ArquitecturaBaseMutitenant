using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Inicio de la organización (ADR 0040, tableros Inicio-Org e Inicio-Miembro): las tarjetas muestran datos reales de
/// cada área y «Para resolver» solo lo que quien mira puede resolver.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class HomeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_home_shows_each_area_with_its_real_data_and_what_to_resolve()
    {
        using var client = NewClient();
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Inicio Delta", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        await OrganizationJourney.CreateCompanyAsync(client, "Delta Norte S.A.", Ct);
        var invitee = $"home-{Guid.NewGuid():N}@example.test";
        await OrganizationJourney.InviteExpiredAsync(factory, client, tenantId, invitee, Ct);
        var domain = $"home-{Guid.NewGuid():N}.example";
        await OrganizationJourney.RegisterDomainAsync(client, domain, Ct);

        var home = await OrganizationJourney.HomeAsync(client, Ct);

        // Las tarjetas dicen lo mismo que cada área.
        var organization = home.GetProperty("organization");
        Assert.Equal(await NumberAsync(client, "/api/users/filter-counts", "all"),
            organization.GetProperty("users").GetProperty("total").GetInt32());
        Assert.Equal(await NumberAsync(client, "/api/roles/paged?page=1&pageSize=10", "totalCount"),
            organization.GetProperty("roles").GetProperty("total").GetInt32());
        Assert.Equal(await NumberAsync(client, "/api/companies/filter-counts", "all"),
            organization.GetProperty("companies").GetProperty("total").GetInt32());
        Assert.Equal("Draft", organization.GetProperty("publicPage").GetProperty("state").GetString());
        Assert.Equal(domain, organization.GetProperty("settings").GetProperty("domain").GetString());
        Assert.Equal(JsonValueKind.String, organization.GetProperty("audit").GetProperty("lastChangeAtUtc").ValueKind);
        Assert.Empty(home.GetProperty("companies").EnumerateArray());

        // Primero lo urgente y después por fecha; la página en borrador no tiene fecha y va al final.
        Assert.Equal(
            [("users", "invitationExpired", invitee), ("settings", "domainUnverified", domain), ("publicPage", "publicPageDraft", null)],
            Pending(home));
    }

    [Fact]
    public async Task Without_permission_the_card_and_the_row_do_not_appear()
    {
        using var ownerClient = NewClient();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Inicio permisos", Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        var companyId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Delta Norte S.A.", Ct);
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenantId, $"home-{Guid.NewGuid():N}@example.test", Ct);
        await OrganizationJourney.RegisterDomainAsync(ownerClient, $"home-{Guid.NewGuid():N}.example", Ct);

        // Un rol que solo ve usuarios: tiene su tarjeta, pero no puede reenviar la invitación ni verificar el dominio.
        using var readerClient = NewClient();
        var reader = await AccountJourney.RegisterAsync(factory, readerClient, Ct);
        var readerMemberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, reader.UserId, Ct);
        using var role = await AccountJourney.PostAsync(ownerClient, "/api/roles", new
        {
            name = "Solo ver usuarios",
            description = (string?)null,
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = new[] { Permissions.Users.Read },
        }, Ct);
        Assert.True(role.StatusCode == HttpStatusCode.Created, await role.Content.ReadAsStringAsync(Ct));
        var roleId = await role.Content.ReadFromJsonAsync<Guid>(Ct);
        await PutAsync(ownerClient, $"/api/users/{readerMemberId:D}", new
        {
            roleIds = new[] { roleId },
            companies = Array.Empty<object>(),
            version = await VersionAsync(ownerClient, $"/api/users/{readerMemberId:D}"),
        });
        await AccountJourney.AuthorizeAsync(readerClient, "business", Ct);

        var readerHome = await OrganizationJourney.HomeAsync(readerClient, Ct);

        var organization = readerHome.GetProperty("organization");
        Assert.Equal(JsonValueKind.Object, organization.GetProperty("users").ValueKind);
        foreach (var area in new[] { "roles", "companies", "publicPage", "settings", "audit" })
            Assert.Equal(JsonValueKind.Null, organization.GetProperty(area).ValueKind);
        Assert.Empty(Pending(readerHome));

        // Alguien con roles solo en una empresa ve «Tus empresas»; esa invitación no es de su empresa, así que no la ve.
        using var memberClient = NewClient();
        var member = await AccountJourney.RegisterAsync(factory, memberClient, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, member.UserId, Ct);
        using var added = await AccountJourney.PostAsync(ownerClient, $"/api/companies/{companyId:D}/members", new
        {
            memberId,
            roleIds = new[] { await CompanyAdminRoleIdAsync(ownerClient) },
        }, Ct);
        Assert.True(added.StatusCode == HttpStatusCode.NoContent, await added.Content.ReadAsStringAsync(Ct));
        await AccountJourney.AuthorizeAsync(memberClient, "business", Ct);

        var memberHome = await OrganizationJourney.HomeAsync(memberClient, Ct);

        Assert.Equal(JsonValueKind.Null, memberHome.GetProperty("organization").ValueKind);
        var company = Assert.Single(memberHome.GetProperty("companies").EnumerateArray());
        Assert.Equal(companyId, company.GetProperty("companyId").GetGuid());
        Assert.Equal("Delta Norte S.A.", company.GetProperty("name").GetString());
        Assert.Equal(["Administrador"], company.GetProperty("roles").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal(2, company.GetProperty("memberCount").GetInt32());
        Assert.Empty(Pending(memberHome));
    }

    // B4, tablero Inicio-Miembro: quien administra una empresa sin roles de la organización ve la invitación vencida de
    // su empresa, con la empresa, y «Reenviar» lo lleva a la ficha de esa empresa, que puede abrir.
    [Fact]
    public async Task A_company_admin_without_organization_roles_sees_the_expired_invitation_of_its_company_and_can_open_it()
    {
        using var ownerClient = NewClient();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Inicio por empresa", Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        var northId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Delta Norte S.A.", Ct);
        var southId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Delta Sur S.A.", Ct);
        var companyAdminRoleId = await CompanyAdminRoleIdAsync(ownerClient);
        var northInvitee = $"home-{Guid.NewGuid():N}@example.test";
        var southInvitee = $"home-{Guid.NewGuid():N}@example.test";
        var organizationInvitee = $"home-{Guid.NewGuid():N}@example.test";
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenantId, northInvitee, Ct,
            [new { companyId = northId, roleIds = new[] { companyAdminRoleId } }]);
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenantId, southInvitee, Ct,
            [new { companyId = southId, roleIds = new[] { companyAdminRoleId } }]);
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenantId, organizationInvitee, Ct);

        using var adminClient = NewClient();
        var admin = await AccountJourney.RegisterAsync(factory, adminClient, Ct);
        var adminMemberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, admin.UserId, Ct);
        using var added = await AccountJourney.PostAsync(ownerClient, $"/api/companies/{northId:D}/members",
            new { memberId = adminMemberId, roleIds = new[] { companyAdminRoleId } }, Ct);
        Assert.True(added.StatusCode == HttpStatusCode.NoContent, await added.Content.ReadAsStringAsync(Ct));
        await AccountJourney.AuthorizeAsync(adminClient, "business", Ct);

        var home = await OrganizationJourney.HomeAsync(adminClient, Ct);

        // Solo la de su empresa, con la empresa: ni la de otra empresa ni la que no es de ninguna.
        Assert.Equal(JsonValueKind.Null, home.GetProperty("organization").ValueKind);
        Assert.Equal([("users", "invitationExpired", northInvitee)], Pending(home));
        var row = Assert.Single(home.GetProperty("pending").EnumerateArray());
        Assert.Equal(northId, row.GetProperty("companyId").GetGuid());
        Assert.Equal("Delta Norte S.A.", row.GetProperty("companyName").GetString());

        // La ficha de su empresa se abre y muestra a la persona invitada; la de otra empresa, no.
        using var company = await adminClient.GetAsync($"/api/companies/{northId:D}", Ct);
        Assert.True(company.StatusCode == HttpStatusCode.OK, await company.Content.ReadAsStringAsync(Ct));
        using var companyJson = JsonDocument.Parse(await company.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Delta Norte S.A.", companyJson.RootElement.GetProperty("name").GetString());
        using var invited = await adminClient.GetAsync(
            $"/api/companies/{northId:D}/members/paged?state=Invited&pageSize=50", Ct);
        Assert.True(invited.StatusCode == HttpStatusCode.OK, await invited.Content.ReadAsStringAsync(Ct));
        using var invitedJson = JsonDocument.Parse(await invited.Content.ReadAsStringAsync(Ct));
        Assert.Contains(invitedJson.RootElement.GetProperty("items").EnumerateArray(),
            member => member.GetProperty("memberId").GetGuid() == row.GetProperty("targetId").GetGuid());
        using var otherCompany = await adminClient.GetAsync($"/api/companies/{southId:D}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, otherCompany.StatusCode);

        // Quien puede reenviar desde Usuarios ve las tres como filas de la organización, sin empresa.
        var ownerHome = await OrganizationJourney.HomeAsync(ownerClient, Ct);
        var ownerRows = ownerHome.GetProperty("pending").EnumerateArray()
            .Where(item => item.GetProperty("kind").GetString() == "invitationExpired").ToArray();
        Assert.Equal(new[] { northInvitee, organizationInvitee, southInvitee }.Order(StringComparer.Ordinal),
            ownerRows.Select(item => item.GetProperty("subject").GetString()!).Order(StringComparer.Ordinal));
        Assert.All(ownerRows, item => Assert.Equal(JsonValueKind.Null, item.GetProperty("companyId").ValueKind));
    }

    internal static (string? Area, string? Kind, string? Subject)[] Pending(JsonElement home) =>
    [
        .. home.GetProperty("pending").EnumerateArray().Select(item => (item.GetProperty("area").GetString(),
            item.GetProperty("kind").GetString(),
            item.GetProperty("subject").ValueKind == JsonValueKind.Null ? null : item.GetProperty("subject").GetString())),
    ];

    private HttpClient NewClient() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<int> NumberAsync(HttpClient client, string path, string property)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty(property).GetInt32();
    }

    private static async Task<uint> VersionAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("version").GetUInt32();
    }

    private static async Task PutAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PutAsJsonAsync(path, body, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<Guid> CompanyAdminRoleIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/roles/paged?scope=Company&page=1&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("items").EnumerateArray()
            .Single(role => role.GetProperty("isSystemRole").GetBoolean() && role.GetProperty("name").GetString() == "Administrador")
            .GetProperty("id").GetGuid();
    }
}
