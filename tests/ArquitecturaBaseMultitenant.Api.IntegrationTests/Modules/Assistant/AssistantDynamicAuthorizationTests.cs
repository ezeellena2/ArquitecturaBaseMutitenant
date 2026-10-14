using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Help;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Help;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantDynamicAuthorizationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Help_suppresses_an_article_when_only_its_permission_is_revoked_during_search()
    {
        var fixture = await BusinessActorAsync(Permissions.Roles.Read);
        var pause = new ReadPause();
        var counter = new NoChat();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new Actor(fixture.UserId, Access.Business)));
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
            DecorateHelp(services, pause);
        }));
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(fixture.TenantId, TenantKind.Business);
        await AssistantExecutionActorTests.AcceptNoticeAsync(provider);
        var tool = Tool(provider, "help_article");
        var before = await tool.ExecuteAsync(Args(new { topicKey = "business.roles" }), Ct);
        Assert.Single(Assert.IsType<AssistantHelpToolData>(before.Value).Articles);
        pause.Arm();
        var execution = tool.ExecuteAsync(Args(new { topicKey = "business.roles" }), Ct);
        await pause.WaitAsync(Ct);
        try { await RemoveAssignmentAsync(fixture, fixture.PermissionRoleId); }
        finally { pause.Resume(); }
        var result = await execution;
        Assert.True(await provider.GetRequiredService<IPermissionService>().HasPermissionAsync(fixture.UserId, Permissions.Assistant.Use, Ct));
        Assert.False(await provider.GetRequiredService<IPermissionService>().HasPermissionAsync(fixture.UserId, Permissions.Roles.Read, Ct));
        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.IsType<AssistantHelpToolData>(result.Value).Articles);
        Assert.Equal(0, counter.Calls);
    }

    [Fact]
    public async Task Help_suppresses_an_article_when_its_catalog_feature_is_disabled_during_search()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await AssistantTestData.ActiveTenantAsync(client);
        await AssistantTestData.PersonalAsync(factory, true);
        var pause = new ReadPause();
        var counter = new NoChat();
        var probe = new ChannelProbe();
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["FeatureManagement:whatsapp"] = "true" }));
            builder.ConfigureTestServices(services =>
            {
                // Desde la E11 la llave whatsapp la declara el módulo instalado; esta prueba la apaga por configuración.
                RegisterHelpChannel(services, true, probe);
                services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new Actor(account.UserId, Access.Consumer)));
                services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
                DecorateChannels(services, probe);
                DecorateHelp(services, pause);
            });
        });
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Personal);
        await AssistantExecutionActorTests.AcceptNoticeAsync(provider);
        var tool = Tool(provider, "help_article");
        var before = await tool.ExecuteAsync(Args(new { topicKey = "whatsapp.signin" }), Ct);
        Assert.Single(Assert.IsType<AssistantHelpToolData>(before.Value).Articles);
        pause.Arm();
        var execution = tool.ExecuteAsync(Args(new { topicKey = "whatsapp.signin" }), Ct);
        await pause.WaitAsync(Ct);
        try { provider.GetRequiredService<IConfiguration>()["FeatureManagement:whatsapp"] = "false"; }
        finally { pause.Resume(); }
        var result = await execution;
        Assert.True(await provider.GetRequiredService<IFeatureService>().IsEnabledAsync("assistant", Ct));
        Assert.False(await provider.GetRequiredService<IFeatureService>().IsEnabledAsync("whatsapp", Ct));
        Assert.True(provider.GetRequiredService<IEnumerable<ILoginCodeChannel>>().Single(row => row.Key == "whatsapp").IsAvailable);
        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.IsType<AssistantHelpToolData>(result.Value).Articles);
        Assert.Equal(0, counter.Calls);
        Assert.Equal(0, probe.SendCalls);
    }

    [Theory]
    [InlineData(false, "es-AR")]
    [InlineData(true, "es-AR")]
    [InlineData(false, "en-US")]
    [InlineData(true, "en-US")]
    public async Task Help_follows_the_whatsapp_module_and_never_uses_its_channel(bool enabled, string culture)
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await AssistantTestData.ActiveTenantAsync(client);
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                var user = (await users.GetByIdAsync(account.UserId, ct))!;
                Assert.True((await users.UpdateProfileAsync(account.UserId, null, null, culture,
                    user.TimeZoneId, user.ProfileVersion, ct)).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AssistantTestData.PersonalAsync(factory, true);
        var pause = new ReadPause();
        var counter = new NoChat();
        var probe = new ChannelProbe();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new Actor(account.UserId, Access.Consumer)));
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
            RegisterHelpChannel(services, enabled, probe);
            DecorateChannels(services, probe);
            DecorateHelp(services, pause);
        }));
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Personal);
        await AssistantExecutionActorTests.AcceptNoticeAsync(provider);
        // Desde la E11 la llave whatsapp es un módulo (para las personas, «Mensajes»): la ayuda sigue al módulo, que manda
        // sobre el canal que comparte su llave; el canal solo se lee y nunca envía.
        Assert.True(await provider.GetRequiredService<IFeatureService>().IsEnabledAsync("whatsapp", Ct));
        var channel = provider.GetRequiredService<IEnumerable<ILoginCodeChannel>>().Single(row => row.Key == "whatsapp");
        Assert.Equal(enabled, channel.IsAvailable);
        var tool = Tool(provider, "help_article");
        var before = await tool.ExecuteAsync(Args(new { topicKey = "whatsapp.signin" }), Ct);
        Assert.True(before.IsSuccess);
        var articles = Assert.IsType<AssistantHelpToolData>(before.Value).Articles;
        Assert.Equal(culture == "en-US" ? "WhatsApp sign-in" : "Ingreso por WhatsApp", Assert.Single(articles).Title);
        pause.Arm();
        var execution = tool.ExecuteAsync(Args(new { topicKey = "whatsapp.signin" }), Ct);
        await pause.WaitAsync(Ct);
        try { probe.Disabled = true; }
        finally { pause.Resume(); }
        var result = await execution;
        Assert.False(channel.IsAvailable);
        Assert.True(await provider.GetRequiredService<IFeatureService>().IsEnabledAsync("assistant", Ct));
        Assert.True(result.IsSuccess);
        Assert.Single(Assert.IsType<AssistantHelpToolData>(result.Value).Articles);
        Assert.Equal(0, counter.Calls);
        Assert.Equal(0, probe.SendCalls);
    }

    [Fact]
    public async Task Pending_keeps_specific_company_scope_and_suppresses_its_revoked_subject_during_read()
    {
        using var ownerClient = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Pending company scope", Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct, tenant);
        var allowed = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Specific allowed", Ct);
        var chatCompany = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Chat projection", Ct);
        var denied = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Specific denied", Ct);
        using var actorClient = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, actorClient, Ct);
        var member = await OrganizationJourney.AddActiveMemberAsync(factory, tenant, account.UserId, Ct);
        await AssistantTestData.EnableAsync(factory, tenant);
        var specific = Role.Create("Specific pending", null, RoleScope.SpecificCompany, allowed, [Permissions.Company.Members.Manage]).Value;
        Guid adminId;
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
            adminId = await services.GetRequiredService<ApplicationDbContext>().Roles.Where(role => role.IsSystemRole && role.Name == SystemRoles.CompanyAdmin).Select(role => role.Id).SingleAsync(Ct);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().AddRange(specific,
                    CompanyMembership.Create(allowed, member).Value, CompanyMembership.Create(chatCompany, member).Value,
                    RoleAssignment.Assign(member, specific.Id, specific.Scope, allowed, allowed).Value,
                    RoleAssignment.Assign(member, adminId, RoleScope.AnyCompany, null, chatCompany).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        var subject = "company-pending-" + Guid.NewGuid().ToString("N") + "@example.test";
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenant, subject, Ct, [new { companyId = allowed, roleIds = new[] { adminId } }]);
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenant, "denied-" + Guid.NewGuid().ToString("N") + "@example.test", Ct, [new { companyId = denied, roleIds = new[] { adminId } }]);
        var pause = new ReadPause();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new Actor(account.UserId, Access.Business)));
            DecorateUsers(services, pause, OrganizationUserState.Expired);
        }));
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
        await AssistantExecutionActorTests.AcceptNoticeAsync(provider);
        var tool = Tool(provider, "org_pending");
        var before = await tool.ExecuteAsync(Args(new { }), Ct);
        Assert.Equal(subject, Assert.Single(Assert.IsType<AssistantPendingToolData>(before.Value).Pending).Subject);
        pause.Arm();
        var execution = tool.ExecuteAsync(Args(new { }), Ct);
        await pause.WaitAsync(Ct);
        try { await RemoveAssignmentAsync(new(tenant, account.UserId, member, specific.Id), specific.Id); }
        finally { pause.Resume(); }
        var result = await execution;
        var permissions = provider.GetRequiredService<IPermissionService>();
        Assert.True(await permissions.HasPermissionAsync(account.UserId, Permissions.Assistant.Use, Ct));
        Assert.True(await permissions.HasCompanyPermissionAsync(account.UserId, chatCompany, Permissions.Company.Members.Manage, Ct));
        Assert.False(await permissions.HasCompanyPermissionAsync(account.UserId, allowed, Permissions.Company.Members.Manage, Ct));
        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.IsType<AssistantPendingToolData>(result.Value).Pending);
    }

    [Theory]
    [InlineData(Permissions.Users.Invite, OrganizationUserState.Expired)]
    [InlineData(Permissions.Users.Manage, OrganizationUserState.PendingDeletion)]
    public async Task Pending_suppresses_subject_when_only_the_source_permission_is_revoked_during_read(
        string permission, OrganizationUserState state)
    {
        var fixture = await BusinessActorAsync(permission);
        if (state == OrganizationUserState.PendingDeletion)
        {
            using var targetClient = AssistantTestData.Client(factory);
            var target = await AccountJourney.RegisterAsync(factory, targetClient, Ct);
            await OrganizationJourney.AddActiveMemberAsync(factory, fixture.TenantId, target.UserId, Ct);
            using var setup = factory.Services.CreateScope();
            var services = setup.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                Assert.True((await services.GetRequiredService<IUserRepository>().RequestDeletionAsync(target.UserId,
                    "Fixture pending", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, 30, ct)).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var pause = new ReadPause();
        var counter = new NoChat();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new Actor(fixture.UserId, Access.Business)));
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
            DecorateUsers(services, pause, state);
        }));
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(fixture.TenantId, TenantKind.Business);
        await AssistantExecutionActorTests.AcceptNoticeAsync(provider);
        var tool = Tool(provider, "org_pending");
        Assert.NotNull(Assert.Single(Assert.IsType<AssistantPendingToolData>((await tool.ExecuteAsync(Args(new { }), Ct)).Value).Pending).Subject);
        pause.Arm();
        var execution = tool.ExecuteAsync(Args(new { }), Ct);
        await pause.WaitAsync(Ct);
        try { await RemoveAssignmentAsync(fixture, fixture.PermissionRoleId); }
        finally { pause.Resume(); }
        var result = await execution;
        Assert.True(await provider.GetRequiredService<IPermissionService>().HasPermissionAsync(fixture.UserId, Permissions.Assistant.Use, Ct));
        Assert.False(await provider.GetRequiredService<IPermissionService>().HasPermissionAsync(fixture.UserId, permission, Ct));
        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.IsType<AssistantPendingToolData>(result.Value).Pending);
        Assert.Equal(0, counter.Calls);
    }

    private async Task<BusinessFixture> BusinessActorAsync(string permission)
    {
        using var ownerClient = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Dynamic tools", Ct);
        if (permission == Permissions.Users.Invite)
        {
            await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct, tenant);
            await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenant, "pending-" + Guid.NewGuid().ToString("N") + "@example.test", Ct);
        }
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var member = await OrganizationJourney.AddActiveMemberAsync(factory, tenant, account.UserId, Ct);
        await AssistantTestData.EnableAsync(factory, tenant);
        var specific = Role.Create("Source permission", null, RoleScope.Organization, null, [permission]).Value;
        var use = Role.Create("Assistant only", null, RoleScope.Organization, null, [Permissions.Assistant.Use]).Value;
        using var setup = factory.Services.CreateScope();
        var services = setup.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().AddRange(specific, use,
                RoleAssignment.Assign(member, use.Id, use.Scope, null, null).Value,
                RoleAssignment.Assign(member, specific.Id, specific.Scope, null, null).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return new(tenant, account.UserId, member, specific.Id);
    }

    private async Task RemoveAssignmentAsync(BusinessFixture fixture, Guid roleId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(fixture.TenantId, TenantKind.Business);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.Remove(await db.RoleAssignments.SingleAsync(row => row.MemberId == fixture.MemberId && row.RoleId == roleId, ct));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static IAssistantTool Tool(IServiceProvider services, string key) => services.GetRequiredService<IEnumerable<IAssistantTool>>().Single(tool => tool.Key == key);
    private static JsonElement Args<T>(T value) => JsonSerializer.SerializeToElement(value);
    private sealed record BusinessFixture(Guid TenantId, Guid UserId, Guid MemberId, Guid PermissionRoleId);
    private sealed record Actor(Guid Id, Access? Access) : ICurrentUser { public Guid? UserId => Id; }

    private static object Original(IServiceProvider services, ServiceDescriptor registration) => registration.ImplementationInstance
        ?? registration.ImplementationFactory?.Invoke(services)
        ?? ActivatorUtilities.CreateInstance(services, registration.ImplementationType!);
    private static void DecorateHelp(IServiceCollection services, ReadPause pause)
    {
        var original = services.Last(row => row.ServiceType == typeof(IHelpArticleReader));
        services.Replace(ServiceDescriptor.Scoped<IHelpArticleReader>(provider => new PausedHelp((IHelpArticleReader)Original(provider, original), pause)));
    }
    private static void DecorateUsers(IServiceCollection services, ReadPause pause, OrganizationUserState state)
    {
        var original = services.Last(row => row.ServiceType == typeof(IOrganizationUserReader));
        services.Replace(ServiceDescriptor.Scoped<IOrganizationUserReader>(provider => new PausedUsers((IOrganizationUserReader)Original(provider, original), pause, state)));
    }
    private static void DecorateChannels(IServiceCollection services, ChannelProbe probe)
    {
        foreach (var original in services.Where(row => row.ServiceType == typeof(ILoginCodeChannel)).ToArray())
        {
            services.Remove(original);
            services.Add(ServiceDescriptor.Describe(typeof(ILoginCodeChannel), provider =>
                new GuardedChannel((ILoginCodeChannel)Original(provider, original), probe), original.Lifetime));
        }
    }
    private static void RegisterHelpChannel(IServiceCollection services, bool available, ChannelProbe probe)
    {
        // This direct-tool host needs only the neutral capability, regardless of installed modules.
        services.RemoveAll<ILoginCodeChannel>();
        services.AddSingleton<ILoginCodeChannel>(new HelpLoginCodeChannel(available, probe));
    }
    private sealed class HelpLoginCodeChannel(bool available, ChannelProbe probe) : ILoginCodeChannel
    {
        public string Key => "whatsapp";
        public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
        public bool IsAvailable => available;
        public IReadOnlyList<string> Countries => [];
        public string? ContactPhone => null;
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => throw probe.UnexpectedSend();
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw probe.UnexpectedSend();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
    }
    private sealed class ChannelProbe
    {
        internal bool Disabled { get; set; }
        internal int SendCalls { get; private set; }
        internal InvalidOperationException UnexpectedSend() { SendCalls++; return new("Help must only read channel availability."); }
    }
    private sealed class GuardedChannel(ILoginCodeChannel inner, ChannelProbe probe) : ILoginCodeChannel
    {
        public string Key => inner.Key;
        public LoginMethodType SupportedMethodType => inner.SupportedMethodType;
        public bool IsAvailable => !probe.Disabled && inner.IsAvailable;
        public IReadOnlyList<string> Countries => inner.Countries;
        public string? ContactPhone => inner.ContactPhone;
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => throw probe.UnexpectedSend();
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw probe.UnexpectedSend();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
    }
    private sealed class ReadPause
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _armed;
        internal void Arm() => _armed = true;
        internal Task WaitAsync(CancellationToken ct) => _started.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        internal void Resume() => _resume.TrySetResult();
        internal async Task AfterReadAsync(CancellationToken ct)
        {
            if (!_armed) return;
            _armed = false;
            _started.TrySetResult();
            await _resume.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }
    }
    private sealed class PausedHelp(IHelpArticleReader inner, ReadPause pause) : IHelpArticleReader
    {
        public IReadOnlyList<HelpArticleMetadata> Catalog => inner.Catalog;
        public async Task<IReadOnlyList<HelpArticleDocument>> SearchAsync(HelpArticleRequest request, HelpArticleContext context, CancellationToken ct)
        {
            var rows = await inner.SearchAsync(request, context, ct);
            await pause.AfterReadAsync(ct);
            return rows;
        }
    }
    private sealed class PausedUsers(IOrganizationUserReader inner, ReadPause pause, OrganizationUserState state) : IOrganizationUserReader
    {
        public async Task<PagedResult<OrganizationUserRow>> ListAsync(ListOrganizationUsersRequest request, DateTime nowUtc, CancellationToken ct)
        {
            var rows = await inner.ListAsync(request, nowUtc, ct);
            if (request.State == state) await pause.AfterReadAsync(ct);
            return rows;
        }
        public Task<OrganizationUserFilterCountsResponse> GetFilterCountsAsync(ListOrganizationUsersRequest request, DateTime nowUtc, CancellationToken ct) => inner.GetFilterCountsAsync(request, nowUtc, ct);
        public Task<OrganizationUserRow?> FindByIdAsync(Guid id, DateTime nowUtc, CancellationToken ct) => inner.FindByIdAsync(id, nowUtc, ct);
    }
    private sealed class NoChat : IChatClientFactory
    {
        internal int Calls { get; private set; }
        public Task<Result<IChatClient>> CreateAsync(CancellationToken ct) { Calls++; throw new InvalidOperationException("No provider in direct tools."); }
        public Task<Result> ValidateKeyAsync(string key, CancellationToken ct) { Calls++; throw new InvalidOperationException("No provider in direct tools."); }
    }
}
