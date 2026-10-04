using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        // Tablero Auditoria-Org: «Roles en la empresa: Sin roles → Solo lectura».
        var change = Assert.Single(entry.GetProperty("changes").EnumerateArray());
        Assert.Equal("Roles en la empresa", change.GetProperty("field").GetString());
        Assert.Equal("Sin roles", change.GetProperty("oldValue").GetString());
        Assert.Equal("Solo lectura", change.GetProperty("newValue").GetString());
    }

    [Fact]
    public async Task Changing_the_roles_of_a_person_in_a_company_leaves_one_entry_with_the_roles_before_and_after()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría roles en empresa");
        var before = await EntryIdsAsync(client);

        // Diálogo «Roles en la empresa» de la ficha de Empresa: quita Ventas y suma Compras en un solo guardado.
        await ChangeCompanyRolesAsync(client, company, company.ReadOnly, company.Purchases);

        // Tablero Auditoria-Org: «Cambió los roles de Tomás Acosta», en Delta S.A., con una sola fila.
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Cambió los roles de Tomás Acosta", entry.GetProperty("actionText").GetString());
        Assert.Equal("Delta S.A.", entry.GetProperty("whereText").GetString());
        var change = Assert.Single(entry.GetProperty("changes").EnumerateArray());
        Assert.Equal("Roles en la empresa", change.GetProperty("field").GetString());
        Assert.Equal("Solo lectura, Ventas", change.GetProperty("oldValue").GetString());
        Assert.Equal("Compras, Solo lectura", change.GetProperty("newValue").GetString());
    }

    [Fact]
    public async Task The_audit_detail_never_shows_an_internal_identifier()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría sin identificadores");
        await ChangeCompanyRolesAsync(client, company, company.Purchases);
        using var invited = await AccountJourney.PostAsync(client, "/api/users/invitations", new
        {
            email = "valentina.ruiz@example.test",
            roleIds = Array.Empty<Guid>(),
            companies = new[] { new { companyId = company.CompanyId, roleIds = new[] { company.ReadOnly } } },
        }, Ct);
        Assert.True(invited.StatusCode == HttpStatusCode.Created, await invited.Content.ReadAsStringAsync(Ct));

        using var response = await client.GetAsync("/api/organization/audit?period=all&limit=50", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(items);

        // Quién, qué y dónde van por nombre: el detalle nunca muestra el identificador de una persona, un rol o una empresa.
        var shown = items.SelectMany(item => new[]
            {
                Text(item, "actionText"), Text(item, "affectedDisplayName"), Text(item, "whereText"),
            }.Concat(item.GetProperty("changes").EnumerateArray().SelectMany(change => new[]
            {
                Text(change, "field"), Text(change, "oldValue"), Text(change, "newValue"),
            })))
            .OfType<string>()
            .ToArray();
        Assert.DoesNotContain(shown, InternalId.IsMatch);
        Assert.Contains("Roles en la empresa", shown);
    }

    private static readonly Regex InternalId = new(
        "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9a-f]{32}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Una empresa con tres roles propios y Tomás Acosta en ella con Ventas y Solo lectura.</summary>
    private async Task<AuditedCompany> StageCompanyMemberAsync(HttpClient client, string organizationName)
    {
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, organizationName, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var companyId = await OrganizationJourney.CreateCompanyAsync(client, "Delta S.A.", Ct);
        var sales = await CreateCompanyRoleAsync(client, "Ventas", companyId);
        var readOnly = await CreateCompanyRoleAsync(client, "Solo lectura", companyId);
        var purchases = await CreateCompanyRoleAsync(client, "Compras", companyId);
        var memberId = await OrganizationJourney.AddNamedMemberAsync(factory, tenantId, "Tomás Acosta", Ct);
        using var added = await AccountJourney.PostAsync(client, $"/api/companies/{companyId:D}/members",
            new { memberId, roleIds = new[] { sales, readOnly } }, Ct);
        Assert.True(added.StatusCode == HttpStatusCode.NoContent, await added.Content.ReadAsStringAsync(Ct));
        return new AuditedCompany(companyId, memberId, sales, readOnly, purchases);
    }

    private static async Task<Guid> CreateCompanyRoleAsync(HttpClient client, string name, Guid companyId)
    {
        using var created = await AccountJourney.PostAsync(client, "/api/roles", new
        {
            name,
            description = (string?)null,
            scope = nameof(RoleScope.SpecificCompany),
            companyId,
            permissions = new[] { Permissions.Company.Members.Read },
        }, Ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        return await created.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    private static async Task ChangeCompanyRolesAsync(HttpClient client, AuditedCompany company, params Guid[] roleIds)
    {
        using var members = await client.GetAsync($"/api/companies/{company.CompanyId:D}/members/paged?pageSize=50", Ct);
        using var json = JsonDocument.Parse(await members.Content.ReadAsStringAsync(Ct));
        var version = json.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("memberId").GetGuid() == company.MemberId)
            .GetProperty("version").GetUInt32();
        using var saved = await client.PutAsJsonAsync(
            $"/api/companies/{company.CompanyId:D}/members/{company.MemberId:D}/roles", new { roleIds, version }, Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.NoContent, await saved.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<HashSet<Guid>> EntryIdsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/organization/audit?period=all&limit=50", Ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. json.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }

    private static async Task<JsonElement[]> NewEntriesAsync(HttpClient client, HashSet<Guid> before)
    {
        using var response = await client.GetAsync("/api/organization/audit?period=all&limit=50", Ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. json.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => !before.Contains(item.GetProperty("id").GetGuid()))
            .Select(item => item.Clone())];
    }

    private sealed record AuditedCompany(Guid CompanyId, Guid MemberId, Guid Sales, Guid ReadOnly, Guid Purchases);

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
    public async Task Someone_who_is_no_longer_a_member_appears_only_with_their_name()
    {
        using var ownerClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenant = await CreateBusinessAsync("Auditoría ex miembros", "auditoria-ex-miembros", owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        var former = ApplicationUser.Create("Tomás Acosta", "es-AR", "America/Argentina/Buenos_Aires").Value;
        var created = await factory.Services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(former);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        var formerEmail = $"tomas-{Guid.NewGuid():N}@example.test";
        var memberId = await SeedActingMemberAsync(tenant.Id, former.Id, formerEmail);

        var whileMember = await ActorEntryAsync(ownerClient, former.Id);
        Assert.Equal(formerEmail, whileMember.GetProperty("actorEmail").GetString());

        await RemoveMemberAsync(tenant.Id, memberId);

        // D7: quien ya no es miembro se muestra solo con su nombre, sin el correo.
        var afterLeaving = await ActorEntryAsync(ownerClient, former.Id);
        Assert.Equal("Tomás Acosta", afterLeaving.GetProperty("actorDisplayName").GetString());
        Assert.True(!afterLeaving.TryGetProperty("actorEmail", out var email) || email.ValueKind == JsonValueKind.Null);
        Assert.DoesNotContain(formerEmail, afterLeaving.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonElement> ActorEntryAsync(HttpClient client, Guid actorId)
    {
        using var response = await client.GetAsync($"/api/organization/audit?period=all&actorId={actorId:D}&limit=50", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return Assert.Single(json.RootElement.GetProperty("items").EnumerateArray()).Clone();
    }

    /// <summary>Suma a la cuenta como miembro activo con un correo verificado y deja una acción suya en la auditoría.</summary>
    private async Task<Guid> SeedActingMemberAsync(Guid tenantId, Guid userId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var member = Member.Invite(userId);
        Assert.True(member.Activate(nowUtc).IsSuccess);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var method = LoginMethod.CreateEmail(userId, Email.Create(email).Value);
            Assert.True(method.Verify(nowUtc).IsSuccess);
            context.LoginMethods.Add(method);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            context.Members.Add(member);
            context.Add(AuditEntry.Create(tenantId, AuditActorKind.User, userId, AuditAction.Created, "Company",
                Guid.CreateVersion7(), "{}", nowUtc));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return member.Id;
    }

    private async Task RemoveMemberAsync(Guid tenantId, Guid memberId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var member = await services.GetRequiredService<ApplicationDbContext>().Members
                .SingleAsync(row => row.Id == memberId, ct);
            Assert.True(member.Remove().IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
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
            var assignment = RoleAssignment.Assign(member.Id, role.Id, role.Scope, role.CompanyId, company.Id).Value;
            context.AddRange(member, company, role, membership, assignment);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }
}
