using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantToolsExecutionTests(ApiFactory factory)
{
    [Fact]
    public async Task Specific_company_permission_never_grants_another_company_and_is_rechecked_after_assignment_removal()
    {
        using var client = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Specific tools", AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "business", AssistantTestData.Ct, tenantId);
        var company = await OrganizationJourney.CreateCompanyAsync(client, "Specific granted", AssistantTestData.Ct);
        var other = await OrganizationJourney.CreateCompanyAsync(client, "Specific denied", AssistantTestData.Ct);
        using var memberClient = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, memberClient, AssistantTestData.Ct);
        var member = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, account.UserId, AssistantTestData.Ct);
        await AssistantTestData.EnableAsync(factory, tenantId);
        var specific = Role.Create("Specific read", null, RoleScope.SpecificCompany, company, [Permissions.Company.Members.Read]).Value;
        var use = Role.Create("Assistant use", null, RoleScope.Organization, null, [Permissions.Assistant.Use]).Value;
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.AddRange(specific, use, CompanyMembership.Create(company, member).Value,
                    RoleAssignment.Assign(member, use.Id, use.Scope, null, null).Value,
                    RoleAssignment.Assign(member, specific.Id, specific.Scope, company, company).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new Actor(account.UserId, Access.Business)))));
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
        await AssistantExecutionActorTests.AcceptNoticeAsync(provider);
        var tool = provider.GetRequiredService<IEnumerable<IAssistantTool>>().Single(row => row.Key == "org_company_members");
        Assert.True((await tool.ExecuteAsync(Args(new { companyId = company, page = 1, pageSize = 20 }), AssistantTestData.Ct)).IsSuccess);
        Assert.Equal(CompanyErrors.NotFound.Code, (await tool.ExecuteAsync(Args(new { companyId = other, page = 1, pageSize = 20 }), AssistantTestData.Ct)).Error.Code);
        await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = provider.GetRequiredService<ApplicationDbContext>();
            db.Remove(await db.RoleAssignments.SingleAsync(row => row.MemberId == member && row.RoleId == specific.Id, ct));
            return Result.Success();
        }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        Assert.True(await provider.GetRequiredService<IPermissionService>().HasPermissionAsync(account.UserId, Permissions.Assistant.Use, AssistantTestData.Ct));
        Assert.True((await tool.ExecuteAsync(Args(new { companyId = company, page = 1, pageSize = 20 }), AssistantTestData.Ct)).IsFailure);
    }
    [Fact]
    public async Task Business_tools_use_real_role_filters_invitation_states_RLS_and_repeat_permissions()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        var own = await OrganizationJourney.StageAsync(factory, account, "Tools own", AssistantTestData.Ct);
        var other = await OrganizationJourney.StageAsync(factory, account, "Tools other", AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "business", AssistantTestData.Ct, own);
        var company = await OrganizationJourney.CreateCompanyAsync(client, "Tools company", AssistantTestData.Ct);
        await OrganizationJourney.AddNamedMemberAsync(factory, own, "Álvarez fixture", AssistantTestData.Ct);
        await OrganizationJourney.InviteExpiredAsync(factory, client, own, "tools-expired-" + Guid.NewGuid().ToString("N") + "@example.test", AssistantTestData.Ct);
        await AssistantTestData.EnableAsync(factory, own);
        Guid otherRole;
        using (var foreign = factory.Services.CreateScope())
        {
            foreign.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(other, TenantKind.Business);
            otherRole = (await foreign.ServiceProvider.GetRequiredService<IRoleService>().ListAsync(new ListRolesRequest { SystemRoleKey = SystemRoles.TenantAdmin, PageSize = 20 }, AssistantTestData.Ct)).Value.Items.Single().Id;
        }
        var actor = new Actor(account.UserId, Access.Business);
        var counter = new NoChat();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(actor));
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
        }));
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(own, TenantKind.Business);
        await AssistantExecutionActorTests.AcceptNoticeAsync(services);
        var tools = services.GetRequiredService<IEnumerable<IAssistantTool>>().ToDictionary(tool => tool.Key);
        var owners = await services.GetRequiredService<IAssistantQuickService>().ExecuteAsync("owners", AssistantTestData.Ct);
        Assert.True(owners.IsSuccess, owners.IsFailure ? owners.Error.Code : "");
        Assert.Single(Assert.IsType<AssistantPeopleQuickDataResponse>(owners.Value.Result.Data).People);
        var found = await tools["org_find_person"].ExecuteAsync(Args(new { search = "alvarez", page = 1, pageSize = 20 }), AssistantTestData.Ct);
        Assert.Equal("Álvarez fixture", Assert.IsType<AssistantPeopleQuickDataResponse>(Assert.IsType<AssistantQuickToolData>(found.Value).Data).People.Single().DisplayName);
        var expired = await tools["org_invitations"].ExecuteAsync(Args(new { status = "Expired", page = 1, pageSize = 20 }), AssistantTestData.Ct);
        Assert.Single(Assert.IsType<AssistantInvitationsQuickDataResponse>(Assert.IsType<AssistantQuickToolData>(expired.Value).Data).Invitations);
        var future = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(1);
        var emptyRange = await tools["org_audit"].ExecuteAsync(Args(new { fromUtc = future, toUtc = future.AddDays(1) }), AssistantTestData.Ct);
        Assert.Empty(Assert.IsType<AssistantAuditQuickDataResponse>(Assert.IsType<AssistantQuickToolData>(emptyRange.Value).Data).Changes);
        var foreignRole = await tools["org_role_members"].ExecuteAsync(Args(new { roleId = otherRole, page = 1, pageSize = 20 }), AssistantTestData.Ct);
        Assert.Equal(RoleErrors.NotFound.Code, foreignRole.Error.Code);
        var companyMembers = await tools["org_company_members"].ExecuteAsync(Args(new { companyId = company, page = 1, pageSize = 20 }), AssistantTestData.Ct);
        Assert.True(companyMembers.IsSuccess);
        var foreignCompany = await tools["org_company_members"].ExecuteAsync(Args(new { companyId = Guid.NewGuid(), page = 1, pageSize = 20 }), AssistantTestData.Ct);
        Assert.Equal(CompanyErrors.NotFound.Code, foreignCompany.Error.Code);
        foreach (var key in new[] { "active_users", "expired_invitations", "recent_changes" })
            Assert.True((await services.GetRequiredService<IAssistantQuickService>().ExecuteAsync(key, AssistantTestData.Ct)).IsSuccess);
        var db = services.GetRequiredService<ApplicationDbContext>();
        var member = await db.Members.SingleAsync(row => row.UserId == account.UserId, AssistantTestData.Ct);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            db.RoleAssignments.RemoveRange(await db.RoleAssignments.Where(row => row.MemberId == member.Id).ToListAsync(ct));
            return Result.Success();
        }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        Assert.False(await tools["org_users"].IsAvailableAsync(AssistantTestData.Ct));
        Assert.True((await tools["org_users"].ExecuteAsync(Args(new { page = 1, pageSize = 20 }), AssistantTestData.Ct)).IsFailure);
        Assert.Equal(0, counter.Calls);
    }

    [Fact]
    public async Task Consumer_outputs_only_own_organizations_distinct_links_masked_methods_and_real_empty_interactions()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        var personal = await AssistantTestData.ActiveTenantAsync(client);
        var one = await OrganizationJourney.StageAsync(factory, account, "Consumer own one", AssistantTestData.Ct);
        var two = await OrganizationJourney.StageAsync(factory, account, "Consumer own two", AssistantTestData.Ct);
        using var foreignClient = AssistantTestData.Client(factory);
        var stranger = await AccountJourney.RegisterAsync(factory, foreignClient, AssistantTestData.Ct);
        var foreign = await OrganizationJourney.StageAsync(factory, stranger, "Consumer foreign", AssistantTestData.Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        var actor = new Actor(account.UserId, Access.Consumer);
        var counter = new NoChat();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(actor));
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
        }));
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(personal, TenantKind.Personal);
        await AssistantExecutionActorTests.AcceptNoticeAsync(services);
        var quick = services.GetRequiredService<IAssistantQuickService>();
        var organizations = await quick.ExecuteAsync("my_organizations", AssistantTestData.Ct);
        Assert.True(organizations.IsSuccess, organizations.IsFailure ? organizations.Error.Code : "");
        var rows = Assert.IsType<AssistantOrganizationsQuickDataResponse>(organizations.Value.Result.Data).Organizations;
        Assert.Equal(new[] { one, two }.Order(), rows.Select(row => row.Id).Order());
        Assert.DoesNotContain(rows, row => row.Id == foreign);
        Assert.Equal(new[] { one, two }.Order(), organizations.Value.Result.Links.Select(link => link.Parameters!.TenantId!.Value).Order());
        var accountResult = await quick.ExecuteAsync("my_signin", AssistantTestData.Ct);
        Assert.True(accountResult.IsSuccess);
        var safe = Assert.IsType<AssistantAccountQuickDataResponse>(accountResult.Value.Result.Data);
        Assert.NotEmpty(safe.Articles);
        Assert.DoesNotContain(safe.LoginMethods, method => method.MaskedValue.Contains(account.Email.Value, StringComparison.Ordinal));
        var messages = await quick.ExecuteAsync("my_messages", AssistantTestData.Ct);
        Assert.True(messages.IsSuccess);
        Assert.Equal("Todavía no tenés mensajes con empresas.", messages.Value.Result.Text);
        Assert.Empty(messages.Value.Result.Links);
        Assert.Empty(Assert.IsType<AssistantInteractionsQuickDataResponse>(messages.Value.Result.Data).Interactions.Items);
        await AssistantTestData.PersonalAsync(factory, false);
        Assert.True((await quick.ExecuteAsync("my_organizations", AssistantTestData.Ct)).IsFailure);
        Assert.Equal(0, counter.Calls);
    }

    [Fact]
    public async Task Platform_direct_queries_read_usage_without_a_provider_and_legal_revalidation_blocks_an_existing_tool()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
        var actor = new Actor(account.UserId, Access.Platform);
        var legal = new MutableLegal();
        var counter = new NoChat();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(actor));
            services.Replace(ServiceDescriptor.Singleton<ILegalAcceptanceService>(legal));
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
        }));
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        await AssistantExecutionActorTests.AcceptNoticeAsync(services);
        var quick = services.GetRequiredService<IAssistantQuickService>();
        foreach (var key in new[] { "pending_organizations", "pending_recoveries", "today_security" })
        {
            var result = await quick.ExecuteAsync(key, AssistantTestData.Ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : "");
        }
        var usage = await quick.ExecuteAsync("assistant_usage", AssistantTestData.Ct);
        Assert.True(usage.IsSuccess, usage.IsFailure ? usage.Error.Code : "");
        Assert.IsType<AssistantUsageQuickDataResponse>(usage.Value.Result.Data);
        Assert.Contains(usage.Value.Result.Links, link => link.RouteKey == AssistantRouteKey.PlatformAssistantUsage);
        var future = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(1);
        var security = services.GetRequiredService<IEnumerable<IAssistantTool>>().Single(row => row.Key == "platform_security");
        var emptyRange = await security.ExecuteAsync(Args(new { fromUtc = future, toUtc = future.AddDays(1) }), AssistantTestData.Ct);
        Assert.Empty(Assert.IsType<AssistantSecurityQuickDataResponse>(Assert.IsType<AssistantQuickToolData>(emptyRange.Value).Data).Events);
        var tool = services.GetRequiredService<IEnumerable<IAssistantTool>>().Single(row => row.Key == "platform_accounts");
        Assert.True(await tool.IsAvailableAsync(AssistantTestData.Ct));
        legal.Pending = true;
        Assert.True((await tool.ExecuteAsync(Args(new { page = 1, pageSize = 20 }), AssistantTestData.Ct)).IsFailure);
        actor.Access = Access.Consumer;
        Assert.False(await tool.IsAvailableAsync(AssistantTestData.Ct));
        Assert.Equal(0, counter.Calls);
    }

    private static JsonElement Args<T>(T value) => JsonSerializer.SerializeToElement(value);
    private sealed class Actor(Guid id, Access access) : ICurrentUser
    {
        public Guid? UserId => id;
        public Access? Access { get; set; } = access;
    }
    private sealed class MutableLegal : ILegalAcceptanceService
    {
        internal bool Pending { get; set; }
        public Task<Result<IReadOnlyList<PendingLegalDocumentResponse>>> GetPendingAsync(CancellationToken ct) =>
            Task.FromResult(Result.Success<IReadOnlyList<PendingLegalDocumentResponse>>(Pending ? [new(Guid.NewGuid(), LegalDocumentKind.Terms, 99)] : []));
        public Task<Result> AcceptAsync(AcceptLegalRequest request, CancellationToken ct) => throw new InvalidOperationException("Read-only fixture.");
    }
    private sealed class NoChat : IChatClientFactory
    {
        internal int Calls { get; private set; }
        public Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken) { Calls++; throw new InvalidOperationException("Direct queries must not call a provider."); }
        public Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken) { Calls++; throw new InvalidOperationException("Direct queries must not call a provider."); }
    }
}

