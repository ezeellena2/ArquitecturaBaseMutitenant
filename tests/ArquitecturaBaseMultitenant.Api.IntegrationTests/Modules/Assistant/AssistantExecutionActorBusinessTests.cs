using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantExecutionActorBusinessTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static JsonElement Empty => JsonSerializer.SerializeToElement(new { });

    [Fact]
    public async Task Consecutive_and_parallel_actors_keep_real_company_permissions_pending_rows_and_audit_owner_separate()
    {
        using var ownerClient = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Actor business company", Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct, tenantId);
        var north = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Actor north", Ct);
        var south = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Actor south", Ct);
        using var roles = await ownerClient.GetFromJsonAsync<JsonDocument>("/api/roles/paged?scope=Company&pageSize=50", Ct);
        var companyRole = roles!.RootElement.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("isSystemRole").GetBoolean()).GetProperty("id").GetGuid();
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenantId, "actor-north-" + Guid.NewGuid().ToString("N") + "@example.test", Ct,
            [new { companyId = north, roleIds = new[] { companyRole } }]);
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenantId, "actor-south-" + Guid.NewGuid().ToString("N") + "@example.test", Ct,
            [new { companyId = south, roleIds = new[] { companyRole } }]);
        using var adminClient = AssistantTestData.Client(factory);
        var admin = await AccountJourney.RegisterAsync(factory, adminClient, Ct);
        var adminMember = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, admin.UserId, Ct);
        using (var added = await AccountJourney.PostAsync(ownerClient, $"/api/companies/{north}/members", new { memberId = adminMember, roleIds = new[] { companyRole } }, Ct))
            Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        using var readerClient = AssistantTestData.Client(factory);
        var reader = await AccountJourney.RegisterAsync(factory, readerClient, Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, reader.UserId, Ct);
        await AssistantTestData.EnableAsync(factory, tenantId);
        await AccountJourney.AuthorizeAsync(adminClient, "business", Ct, tenantId);
        await AssistantExecutionActorTests.AcceptNoticeAsync(adminClient, "/api/organization/assistant/notice");
        await AccountJourney.AuthorizeAsync(readerClient, "business", Ct, tenantId);
        var adminActor = await AssistantExecutionActorTests.CaptureAsync(adminClient);
        var readerActor = await AssistantExecutionActorTests.CaptureAsync(readerClient);

        async Task Verify(IServiceProvider services, ExecutionActor original, bool allowed)
        {
            var bound = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(original, Ct);
            Assert.True(bound.IsSuccess, bound.Error.Code);
            using (services.GetRequiredService<IExecutionActorScope>().Enter(bound.Value))
            {
                Assert.Equal(TenantKind.Business, services.GetRequiredService<ITenantContext>().TenantKind);
                Assert.Equal((AuditActorKind.User, (Guid?)original.OwnerUserId), AuditActorResolver.Resolve(services.GetRequiredService<ICurrentUser>()));
                var permissions = services.GetRequiredService<IPermissionService>();
                Assert.Equal(allowed, await permissions.HasPermissionAsync(original.OwnerUserId, Permissions.Assistant.Use, Ct));
                Assert.Equal(allowed, await permissions.HasCompanyPermissionAsync(original.OwnerUserId, north, Permissions.Company.Members.Manage, Ct));
                Assert.False(await permissions.HasCompanyPermissionAsync(original.OwnerUserId, south, Permissions.Company.Members.Manage, Ct));
                var tool = services.GetRequiredService<IEnumerable<IAssistantTool>>().Single(row => row.Key == "org_pending");
                var result = await tool.ExecuteAsync(Empty, Ct);
                Assert.Equal(allowed, result.IsSuccess);
                if (allowed)
                {
                    Assert.Equal("users", Assert.Single(Assert.IsType<AssistantPendingToolData>(result.Value).Pending).Area);
                    var companyTool = services.GetRequiredService<IEnumerable<IAssistantTool>>().Single(row => row.Key == "org_company_members");
                    Assert.True((await companyTool.ExecuteAsync(JsonSerializer.SerializeToElement(new { companyId = north, page = 1, pageSize = 20 }), Ct)).IsSuccess);
                    Assert.Equal(CompanyErrors.NotFound.Code, (await companyTool.ExecuteAsync(JsonSerializer.SerializeToElement(new { companyId = south, page = 1, pageSize = 20 }), Ct)).Error.Code);
                }
            }
            Assert.Null(services.GetRequiredService<ICurrentUser>().UserId);
            Assert.Null(services.GetRequiredService<ITenantContext>().TenantId);
        }

        using (var sequential = factory.Services.CreateScope())
        {
            await Verify(sequential.ServiceProvider, adminActor, true);
            await Verify(sequential.ServiceProvider, readerActor, false);
            await Verify(sequential.ServiceProvider, adminActor, true);
        }
        async Task Parallel(ExecutionActor actor, bool allowed)
        {
            using var scope = factory.Services.CreateScope();
            await Verify(scope.ServiceProvider, actor, allowed);
        }
        await Task.WhenAll(Parallel(adminActor, true), Parallel(readerActor, false));
    }

    [Theory]
    [InlineData("account")]
    [InlineData("organization")]
    [InlineData("membership")]
    [InlineData("permission")]
    [InlineData("feature")]
    [InlineData("emergency")]
    public async Task Revocation_after_capture_blocks_the_next_tool_phase_with_real_core_policies(string revocation)
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, account, "Actor revoked " + revocation, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        await AssistantTestData.EnableAsync(factory, tenantId);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/organization/assistant/notice");
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var prepared = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        Assert.True(prepared.IsSuccess);
        using (services.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value))
        {
            var tool = services.GetRequiredService<IEnumerable<IAssistantTool>>().Single(row => row.Key == "org_pending");
            Assert.True((await tool.ExecuteAsync(Empty, Ct)).IsSuccess);
            var configuration = factory.Services.GetRequiredService<IConfiguration>();
            var previous = configuration["FeatureManagement:assistant"];
            try
            {
                if (revocation == "emergency") configuration["FeatureManagement:assistant"] = "false";
                else if (revocation == "feature")
                {
                    using var mutation = factory.Services.CreateScope();
                    await mutation.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                    {
                        var db = mutation.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var grant = await db.TenantFeatures.SingleAsync(row => row.TenantId == tenantId && row.FeatureKey == "assistant", ct);
                        Assert.True(grant.Update(false, grant.Source, grant.ExpiresAtUtc).IsSuccess);
                        mutation.ServiceProvider.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(
                            AuditActorKind.System, Guid.Empty, tenantId, "Actor fixture revocation", mutation.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                        return Result.Success();
                    }, CommitPolicy.OnSuccess, Ct);
                    await mutation.ServiceProvider.GetRequiredService<Application.Interfaces.Integrations.Caching.ITenantFeatureCache>().InvalidateAsync(tenantId, Ct);
                }
                else
                {
                    using var mutation = factory.Services.CreateScope();
                    var provider = mutation.ServiceProvider;
                    provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
                    await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                    {
                        var db = provider.GetRequiredService<ApplicationDbContext>();
                        if (revocation == "account") Assert.True((await provider.GetRequiredService<IUserRepository>().SuspendAsync(account.UserId, ct)).IsSuccess);
                        if (revocation == "organization") Assert.True((await db.Tenants.SingleAsync(row => row.Id == tenantId, ct)).Suspend().IsSuccess);
                        var member = await db.Members.SingleAsync(row => row.UserId == account.UserId, ct);
                        if (revocation == "membership") Assert.True(member.Deactivate().IsSuccess);
                        if (revocation == "permission") db.RemoveRange(await db.RoleAssignments.Where(row => row.MemberId == member.Id).ToArrayAsync(ct));
                        return Result.Success();
                    }, CommitPolicy.OnSuccess, Ct);
                }
                Assert.True((await tool.ExecuteAsync(Empty, Ct)).IsFailure);
            }
            finally { configuration["FeatureManagement:assistant"] = previous; }
        }
        var rebound = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        Assert.Equal(revocation is not ("account" or "organization" or "membership"), rebound.IsSuccess);
    }
}
