using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

[Collection(ApiTestGroup.Name)]
public sealed class OrganizationAuditTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Audit_returns_affected_person_and_company_for_role_changes()
    {
        using var ownerClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenant = await CreateBusinessAsync("Auditoría ubicación", "auditoria-ubicacion", owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);

        var target = ApplicationUser.Create("Tomás Acosta", "es-AR", "America/Argentina/Buenos_Aires").Value;
        var userManager = factory.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await userManager.CreateAsync(target);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        await SeedCompanyRoleAssignmentAsync(tenant.Id, target.Id);

        using var response = await ownerClient.GetAsync("/api/organization/audit?period=all&category=roles&limit=50", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var entry = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.TryGetProperty("affectedDisplayName", out var name) && name.GetString() == "Tomás Acosta");

        Assert.Equal("Cambió los roles de Tomás Acosta", entry.GetProperty("actionText").GetString());
        Assert.Equal("Delta S.A.", entry.GetProperty("whereText").GetString());
    }

    [Fact]
    public async Task Audit_detail_shows_who_acted_with_name_and_email()
    {
        using var ownerClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        await CreateBusinessAsync("Auditoría quién", "auditoria-quien", owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        using var created = await AccountJourney.PostAsync(ownerClient, "/api/companies", new
        {
            name = "Delta Norte S.A.",
            taxIdType = (string?)null,
            taxIdNumber = (string?)null,
            timeZoneId = "America/Argentina/Buenos_Aires",
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var response = await ownerClient.GetAsync("/api/organization/audit?period=all&category=companies&limit=50", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var entry = json.RootElement.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("actorKind").GetString() == "User");

        // Tablero Auditoria-Org, detalle: «Quién: Lucía Fernández · lucia.fernandez@delta.ejemplo.com».
        Assert.Equal(owner.Email.Value, entry.GetProperty("actorEmail").GetString());
    }

    [Fact]
    public async Task Audit_is_tenant_scoped_searchable_paginated_and_masks_personal_data()
    {
        using var ownerClient = Client();
        using var otherClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var other = await AccountJourney.RegisterAsync(factory, otherClient, Ct);
        var tenant = await CreateBusinessAsync("Auditoría test", "auditoria-test", owner.UserId);
        var otherTenant = await CreateBusinessAsync("Otra auditoría test", "otra-auditoria", other.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        await AccountJourney.AuthorizeAsync(otherClient, "business", Ct);
        await SeedEntriesAsync(tenant.Id, 49);
        await SeedEntriesAsync(otherTenant.Id, 1);

        using var firstPage = await ownerClient.GetAsync("/api/organization/audit?period=all&category=users&limit=10", Ct);
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        using var firstJson = JsonDocument.Parse(await firstPage.Content.ReadAsStringAsync(Ct));
        var first = firstJson.RootElement;
        Assert.Equal(10, first.GetProperty("items").GetArrayLength());
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        var cursor = first.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));

        using var secondPage = await ownerClient.GetAsync(
            $"/api/organization/audit?period=all&category=users&limit=50&after={Uri.EscapeDataString(cursor!)}", Ct);
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        using var secondJson = JsonDocument.Parse(await secondPage.Content.ReadAsStringAsync(Ct));
        var second = secondJson.RootElement;
        Assert.Equal(40, second.GetProperty("items").GetArrayLength());
        Assert.False(second.GetProperty("hasMore").GetBoolean());
        var ids = first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .Concat(second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()))
            .ToArray();
        Assert.Equal(50, ids.Distinct().Count());

        using var search = await ownerClient.GetAsync(
            "/api/organization/audit?period=all&category=users&search=private-004%40example.test", Ct);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync(Ct));
        var result = searchJson.RootElement.GetProperty("items");
        var match = Assert.Single(result.EnumerateArray());
        Assert.DoesNotContain("private-004@example.test", match.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("***@example.test", match.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("***8900", match.GetRawText(), StringComparison.Ordinal);

        using var actors = await ownerClient.GetAsync("/api/organization/audit/actors?period=all&category=users", Ct);
        Assert.Equal(HttpStatusCode.OK, actors.StatusCode);
        using var actorsJson = JsonDocument.Parse(await actors.Content.ReadAsStringAsync(Ct));
        Assert.NotEmpty(actorsJson.RootElement.EnumerateArray());
        Assert.DoesNotContain("private-004@example.test", actorsJson.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);

        using var unauthorized = Client();
        using var denied = await unauthorized.GetAsync("/api/organization/audit", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    private HttpClient Client() => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    private async Task<Tenant> CreateBusinessAsync(string name, string slug, Guid userId)
    {
        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        Assert.True(tenant.SetSlug(TenantSlug.Create(slug).Value).IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId], userId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant;
    }

    private async Task SeedEntriesAsync(Guid tenantId, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            var audit = services.GetRequiredService<IAuditLog>();
            for (var index = 0; index < count; index++)
            {
                var email = $"private-{index:000}@example.test";
                audit.Record(AuditAction.Custom, "Member", Guid.NewGuid(), new Dictionary<string, object?>
                {
                    ["Email"] = new Dictionary<string, object?> { ["old"] = null, ["new"] = email },
                    ["Phone"] = new Dictionary<string, object?> { ["old"] = null, ["new"] = "+541145678900" },
                });
            }

            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task SeedCompanyRoleAssignmentAsync(Guid tenantId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var context = services.GetRequiredService<ApplicationDbContext>();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var member = Member.Invite(userId);
            Assert.True(member.Activate(nowUtc).IsSuccess);
            var company = Company.Create("Delta S.A.", null, "America/Argentina/Buenos_Aires").Value;
            var role = Role.Create("Solo lectura", null, RoleScope.AnyCompany, null,
                [Permissions.Company.Members.Read]).Value;
            var membership = CompanyMembership.Create(company.Id, member.Id).Value;
            var assignment = RoleAssignment.Assign(member.Id, role, company.Id).Value;
            context.AddRange(member, company, role, membership, assignment);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }
}
