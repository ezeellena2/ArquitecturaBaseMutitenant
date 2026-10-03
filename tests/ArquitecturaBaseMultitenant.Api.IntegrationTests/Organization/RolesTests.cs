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
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>Comprueba el contrato HTTP de Roles con una sesión real y una base PostgreSQL aislada.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class RolesTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Filtering_by_a_company_lists_its_roles_and_the_ones_valid_in_every_company()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = Tenant.CreateBusiness("Roles por empresa", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var north = await CreateCompanyAsync(client, "Delta Norte S.A.");
        var south = await CreateCompanyAsync(client, "Delta Sur S.R.L.");
        foreach (var (companyId, name) in new[] { (north, "Solo lectura Norte"), (south, "Solo lectura Sur") })
        {
            using var created = await AccountJourney.PostAsync(client, "/api/roles", new
            {
                name,
                description = (string?)null,
                scope = "SpecificCompany",
                companyId,
                permissions = new[] { Permissions.Company.Members.Read },
            }, Ct);
            Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        }

        // Tablero Roles, filtro «Vale en: Delta Norte S.A.»: su «Solo lectura» y el Administrador de «Cada empresa».
        using var response = await client.GetAsync($"/api/roles/paged?companyId={north:D}&pageSize=50", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var roles = json.RootElement.GetProperty("items").EnumerateArray()
            .Select(role => (Scope: role.GetProperty("scope").GetString(),
                Company: role.TryGetProperty("companyId", out var company) && company.ValueKind == JsonValueKind.String ? company.GetGuid() : (Guid?)null))
            .ToArray();
        Assert.Contains(roles, role => role.Scope == nameof(RoleScope.SpecificCompany) && role.Company == north);
        Assert.Contains(roles, role => role.Scope == nameof(RoleScope.AnyCompany));
        Assert.DoesNotContain(roles, role => role.Company == south);
        Assert.DoesNotContain(roles, role => role.Scope == nameof(RoleScope.Organization));
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name)
    {
        using var created = await AccountJourney.PostAsync(client, "/api/companies", new
        {
            name,
            taxIdType = (string?)null,
            taxIdNumber = (string?)null,
            timeZoneId = "America/Argentina/Buenos_Aires",
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return await created.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    [Fact]
    public async Task Tenant_admin_reads_permissions_and_manages_roles_with_optimistic_concurrency()
    {
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = Tenant.CreateBusiness("Roles HTTP test", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        await AccountJourney.AuthorizeAsync(client, "business", Ct);

        using var meResponse = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        using var meJson = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync(Ct));
        Assert.Contains(meJson.RootElement.GetProperty("effectivePermissions").GetProperty("organization")
            .EnumerateArray().Select(permission => permission.GetString()),
            permission => permission == Permissions.Roles.Manage);

        using var permissionsResponse = await client.GetAsync("/api/permissions", Ct);
        Assert.Equal(HttpStatusCode.OK, permissionsResponse.StatusCode);
        using var permissionJson = JsonDocument.Parse(await permissionsResponse.Content.ReadAsStringAsync(Ct));
        Assert.Contains(permissionJson.RootElement.EnumerateArray()
            .SelectMany(group => group.GetProperty("permissions").EnumerateArray())
            .Select(permission => permission.GetProperty("code").GetString()),
            code => code == Permissions.Roles.Manage);

        using var listResponse = await client.GetAsync("/api/roles/paged?page=1&pageSize=10&search=TenantAdmin", Ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var pageJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(Ct));
        var ownerRole = Assert.Single(pageJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.True(ownerRole.GetProperty("isSystemRole").GetBoolean());
        // Tablero Roles: «Dueño · Tiene todos los permisos de la organización.»
        Assert.Equal("Tiene todos los permisos de la organización.", ownerRole.GetProperty("description").GetString());
        Assert.Equal(1, ownerRole.GetProperty("userCount").GetInt32());
        var ownerRoleId = ownerRole.GetProperty("id").GetGuid();

        using var organizationRolesResponse = await client.GetAsync("/api/roles/paged?scope=Organization", Ct);
        Assert.Equal(HttpStatusCode.OK, organizationRolesResponse.StatusCode);
        using var organizationRolesJson = JsonDocument.Parse(await organizationRolesResponse.Content.ReadAsStringAsync(Ct));
        Assert.All(organizationRolesJson.RootElement.GetProperty("items").EnumerateArray(), role =>
            Assert.Equal(nameof(RoleScope.Organization), role.GetProperty("scope").GetString()));

        using var companyRolesResponse = await client.GetAsync("/api/roles/paged?scope=Company", Ct);
        Assert.Equal(HttpStatusCode.OK, companyRolesResponse.StatusCode);
        using var companyRolesJson = JsonDocument.Parse(await companyRolesResponse.Content.ReadAsStringAsync(Ct));
        Assert.All(companyRolesJson.RootElement.GetProperty("items").EnumerateArray(), role =>
            Assert.NotEqual(nameof(RoleScope.Organization), role.GetProperty("scope").GetString()));
        Assert.NotEmpty(companyRolesJson.RootElement.GetProperty("items").EnumerateArray());

        var roleName = "Lectura " + Guid.NewGuid().ToString("N");
        using var created = await AccountJourney.PostAsync(client, "/api/roles", new
        {
            name = roleName,
            description = "Rol para el contrato de Roles",
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = new[] { Permissions.Roles.Read },
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var rolePath = created.Headers.Location!.IsAbsoluteUri
            ? created.Headers.Location.PathAndQuery
            : created.Headers.Location.OriginalString;
        using var detail = await client.GetAsync(rolePath, Ct);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(Ct));
        Assert.Equal(roleName, detailJson.RootElement.GetProperty("name").GetString());
        var version = detailJson.RootElement.GetProperty("version").GetUInt32();

        using var missingVersion = await client.PutAsJsonAsync(rolePath, new
        {
            name = roleName + " editado",
            description = "Rol para el contrato de Roles",
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = new[] { Permissions.Roles.Read },
        }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);

        using var updated = await client.PutAsJsonAsync(rolePath, new
        {
            name = roleName + " editado",
            description = "Rol para el contrato de Roles",
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = new[] { Permissions.Roles.Read },
            version,
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        using var staleUpdate = await client.PutAsJsonAsync(rolePath, new
        {
            name = roleName + " obsoleto",
            description = "Rol para el contrato de Roles",
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = new[] { Permissions.Roles.Read },
            version,
        }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
        using var staleProblem = JsonDocument.Parse(await staleUpdate.Content.ReadAsStringAsync(Ct));
        Assert.Equal("General.ConcurrencyConflict", staleProblem.RootElement.GetProperty("code").GetString());

        using var currentDetail = await client.GetAsync(rolePath, Ct);
        using var currentJson = JsonDocument.Parse(await currentDetail.Content.ReadAsStringAsync(Ct));
        var currentVersion = currentJson.RootElement.GetProperty("version").GetUInt32();
        using var staleDelete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, rolePath)
        {
            Content = JsonContent.Create(new { version }),
        }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);

        using var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, rolePath)
        {
            Content = JsonContent.Create(new { version = currentVersion }),
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var systemRolePath = $"/api/roles/{ownerRoleId:D}";
        using var systemDelete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, systemRolePath)
        {
            Content = JsonContent.Create(new { version = ownerRole.GetProperty("version").GetUInt32() }),
        }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, systemDelete.StatusCode);
    }
}
