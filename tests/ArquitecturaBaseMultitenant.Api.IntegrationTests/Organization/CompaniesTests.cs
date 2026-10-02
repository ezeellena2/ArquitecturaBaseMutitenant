using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>Recorre el CRUD versionado de empresas con identidad, permisos y PostgreSQL reales.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class CompaniesTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
            taxIdType = (string?)null,
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

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            var companyMember = Member.Invite(otherAccount.UserId);
            Assert.True(companyMember.Activate(new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);
            var companyMembership = CompanyMembership.Create(companyId, companyMember.Id).Value;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var role = await services.GetRequiredService<IRoleRepository>()
                    .GetByNameAsync(SystemRoles.CompanyAdmin, ct);
                Assert.NotNull(role);
                var assignment = RoleAssignment.Assign(companyMember.Id, role, companyId);
                if (assignment.IsFailure) return assignment.Error;
                services.GetRequiredService<IMemberRepository>().Add(companyMember);
                services.GetRequiredService<ICompanyMembershipRepository>().Add(companyMembership);
                services.GetRequiredService<IRoleAssignmentRepository>().Add(assignment.Value);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            var permissions = await services.GetRequiredService<IPermissionService>()
                .GetCompanyPermissionsAsync(otherAccount.UserId, companyId, Ct);
            Assert.Contains(Permissions.Company.Members.Read, permissions);
        }

        using var updated = await client.PutAsJsonAsync(detailPath, new
        {
            name = "Sucursal Centro II",
            taxIdType = (string?)null,
            taxIdNumber = (string?)null,
            timeZoneId = "America/Argentina/Buenos_Aires",
            version = createdVersion,
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        using var staleUpdate = await client.PutAsJsonAsync(detailPath, new
        {
            name = "Versión obsoleta",
            taxIdType = (string?)null,
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
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync(Ct));
        var listed = Assert.Single(listJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(companyId, listed.GetProperty("id").GetGuid());
        Assert.Equal(CompanyStatus.Inactive.ToString(), listed.GetProperty("status").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            var permissions = await scope.ServiceProvider.GetRequiredService<IPermissionService>()
                .GetCompanyPermissionsAsync(otherAccount.UserId, companyId, Ct);
            Assert.Empty(permissions);
        }

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
