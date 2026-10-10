using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantAuthorizationTests(ApiFactory factory)
{
    [Theory]
    [InlineData("company")]
    [InlineData("membership")]
    [InlineData("member")]
    [InlineData("assignment")]
    public async Task Only_explicit_projectable_use_is_granted_and_revocation_is_live(string revocation)
    {
        using var ownerClient = AssistantTestData.Client(factory);
        using var adminClient = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, AssistantTestData.Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Assistant permissions", AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", AssistantTestData.Ct, tenantId);
        var companyId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Assistant company", AssistantTestData.Ct);
        var unassignedCompany = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Unassigned company", AssistantTestData.Ct);
        using var roles = await ownerClient.GetFromJsonAsync<JsonDocument>("/api/roles/paged?scope=Company&pageSize=50", AssistantTestData.Ct);
        var roleId = roles!.RootElement.GetProperty("items").EnumerateArray().Single(value => value.GetProperty("isSystemRole").GetBoolean()).GetProperty("id").GetGuid();
        var admin = await AccountJourney.RegisterAsync(factory, adminClient, AssistantTestData.Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, admin.UserId, AssistantTestData.Ct);
        using var added = await AccountJourney.PostAsync(ownerClient, $"/api/companies/{companyId}/members", new { memberId, roleIds = new[] { roleId } }, AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        await AssistantTestData.EnableAsync(factory, tenantId);
        var otherTenantId = await OrganizationJourney.StageAsync(factory, owner, "Assistant permissions other", AssistantTestData.Ct);
        using (var other = factory.Services.CreateScope())
        {
            other.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(otherTenantId, TenantKind.Business);
            Assert.Empty(await other.ServiceProvider.GetRequiredService<IPermissionService>().GetOrganizationPermissionsAsync(admin.UserId, AssistantTestData.Ct));
        }
        await AccountJourney.AuthorizeAsync(adminClient, "business", AssistantTestData.Ct, tenantId);
        using var directHost = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new ToolActor(admin.UserId)))));
        using var directScope = directHost.Services.CreateScope();
        directScope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
        await AssistantExecutionActorTests.AcceptNoticeAsync(directScope.ServiceProvider);
        var directTool = directScope.ServiceProvider.GetRequiredService<IEnumerable<IAssistantTool>>().Single(tool => tool.Key == "org_company_members");
        var directArguments = JsonSerializer.SerializeToElement(new { companyId, page = 1, pageSize = 20 });
        Assert.True((await directTool.ExecuteAsync(directArguments, AssistantTestData.Ct)).IsSuccess);
        var unassigned = await directTool.ExecuteAsync(JsonSerializer.SerializeToElement(new { companyId = unassignedCompany, page = 1, pageSize = 20 }), AssistantTestData.Ct);
        Assert.Equal(CompanyErrors.NotFound.Code, unassigned.Error.Code);
        Assert.False(await directScope.ServiceProvider.GetRequiredService<IEnumerable<IAssistantTool>>().Single(tool => tool.Key == "org_users").IsAvailableAsync(AssistantTestData.Ct));
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/organization/assistant/conversations", AssistantTestData.Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.GetAsync("/api/organization/assistant/settings", AssistantTestData.Ct)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
            var permission = services.GetRequiredService<IPermissionService>();
            Assert.Equal(new[] { Permissions.Assistant.Use }, await permission.GetOrganizationPermissionsAsync(admin.UserId, AssistantTestData.Ct));
            Assert.Contains(Permissions.Company.Members.Read, await permission.GetCompanyPermissionsAsync(admin.UserId, companyId, AssistantTestData.Ct));
            var db = services.GetRequiredService<ApplicationDbContext>();
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                switch (revocation)
                {
                    case "company": (await db.Companies.SingleAsync(value => value.Id == companyId, ct)).SetStatus(CompanyStatus.Inactive); break;
                    case "membership": (await db.CompanyMemberships.SingleAsync(value => value.CompanyId == companyId && value.MemberId == memberId, ct)).SetStatus(CompanyMembershipStatus.Inactive); break;
                    case "member": Assert.True((await db.Members.SingleAsync(value => value.Id == memberId, ct)).Deactivate().IsSuccess); break;
                    default: db.RoleAssignments.Remove(await db.RoleAssignments.SingleAsync(value => value.MemberId == memberId && value.RoleId == roleId, ct)); break;
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        using (var check = factory.Services.CreateScope())
        {
            check.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
            Assert.Empty(await check.ServiceProvider.GetRequiredService<IPermissionService>().GetOrganizationPermissionsAsync(admin.UserId, AssistantTestData.Ct));
        }
        using var denied = await adminClient.GetAsync("/api/organization/assistant/conversations", AssistantTestData.Ct);
        Assert.Contains(denied.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized });
        Assert.True((await directTool.ExecuteAsync(directArguments, AssistantTestData.Ct)).IsFailure);
    }

    private sealed class ToolActor(Guid id) : ICurrentUser
    {
        public Guid? UserId => id;
        public Access? Access => Domain.Users.Access.Business;
    }
}
