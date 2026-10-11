using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantStateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("consumer", "/api/me/assistant", false)]
    [InlineData("business", "/api/organization/assistant", true)]
    [InlineData("platform", "/api/platform/assistant", true)]
    public async Task Enabled_access_without_a_key_returns_unconfigured_state_before_notice_acceptance(
        string access, string path, bool canConfigure)
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var probe = new StateProbe();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var original = services.Single(row => row.ServiceType == typeof(IUnitOfWork));
            services.Replace(ServiceDescriptor.Scoped<IUnitOfWork>(provider => new ObservedUnitOfWork(
                (IUnitOfWork)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!), probe)));
            services.RemoveAll<IChatClientFactory>();
            services.AddScoped<IChatClientFactory>(_ => new ObservedClients(probe));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        if (access == "consumer") await AssistantTestData.PersonalAsync(factory, true);
        else if (access == "business")
        {
            var tenantId = await OrganizationJourney.StageAsync(factory, account, "State organization", Ct);
            await AssistantTestData.EnableAsync(factory, tenantId);
            await AccountJourney.AuthorizeAsync(client, access, Ct, tenantId);
        }
        else
        {
            await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
            await AccountJourney.AuthorizeAsync(client, access, Ct);
        }

        probe.ReadingState = true;
        using var response = await client.GetAsync(path, Ct);
        probe.ReadingState = false;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var state = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.False(state.RootElement.GetProperty("configured").GetBoolean());
        Assert.Equal(canConfigure, state.RootElement.GetProperty("canConfigure").GetBoolean());
        Assert.False(state.RootElement.GetProperty("noticeAccepted").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(state.RootElement.GetProperty("noticeVersion").GetString()));
        Assert.Empty(state.RootElement.GetProperty("quickQueries").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("monthlyBudgetProgress").ValueKind);
        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("monthlyBudget").ValueKind);
        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("usedCost").ValueKind);
        Assert.Equal(50, state.RootElement.GetProperty("dailyLimit").GetInt32());
        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("remainingMessages").ValueKind);
        Assert.EndsWith("Z", state.RootElement.GetProperty("dailyResetAtUtc").GetString());
        Assert.Equal(30, state.RootElement.GetProperty("retentionDays").GetInt32());
        Assert.Equal(10485760, state.RootElement.GetProperty("maxAttachmentBytes").GetInt64());
        Assert.Equal(5, state.RootElement.GetProperty("maxAttachments").GetInt32());
        AssertModels(state.RootElement);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Set<AssistantNoticeAcceptance>().AnyAsync(row => row.OwnerUserId == account.UserId, Ct));
        }
        Assert.Equal(0, probe.Transactions);
        Assert.Equal(0, probe.ClientCalls);

        await AssistantExecutionActorTests.AcceptNoticeAsync(client, path + "/notice");
        probe.ReadingState = true;
        using var after = await ReadAsync(client, path);
        probe.ReadingState = false;
        Assert.True(after.RootElement.GetProperty("noticeAccepted").GetBoolean());
        Assert.False(after.RootElement.GetProperty("configured").GetBoolean());
        var expected = access switch
        {
            "consumer" => new[] { "my_organizations", "my_signin", "my_messages" },
            "business" => ["active_users", "expired_invitations", "owners", "recent_changes"],
            _ => ["pending_organizations", "pending_recoveries", "today_security", "assistant_usage"],
        };
        Assert.Equal(expected.Order(), QuickKeys(after.RootElement).Order());
        Assert.All(after.RootElement.GetProperty("quickQueries").EnumerateArray(), item =>
            Assert.Equal("Quick." + item.GetProperty("key").GetString(), item.GetProperty("labelKey").GetString()));
        Assert.Equal(state.RootElement.GetProperty("remainingMessages").ValueKind,
            after.RootElement.GetProperty("remainingMessages").ValueKind);
        Assert.Equal(0, probe.Transactions);
        Assert.Equal(0, probe.ClientCalls);
        using (var scope = host.Services.CreateScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Set<AssistantNoticeAcceptance>().CountAsync(row => row.OwnerUserId == account.UserId, Ct));
    }

    [Fact]
    public async Task Platform_use_permission_alone_never_confers_configuration_or_tool_permissions_and_revocation_is_immediate()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Assistant.Use], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/platform/assistant/notice");

        using var state = await ReadAsync(client, "/api/platform/assistant");
        Assert.False(state.RootElement.GetProperty("canConfigure").GetBoolean());
        Assert.True(state.RootElement.GetProperty("noticeAccepted").GetBoolean());
        Assert.Empty(QuickKeys(state.RootElement));
        using var settings = await client.GetAsync("/api/platform/assistant/settings", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, settings.StatusCode);

        await PlatformOperators.GrantAsync(factory.Services, account.UserId,
            [PlatformPermissions.Assistant.Use, PlatformPermissions.Tenants.Read], Ct);
        using var expanded = await ReadAsync(client, "/api/platform/assistant");
        Assert.Equal(["pending_organizations"], QuickKeys(expanded.RootElement));
        Assert.False(expanded.RootElement.GetProperty("canConfigure").GetBoolean());

        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Assistant.Configure], Ct);
        using var revoked = await client.GetAsync("/api/platform/assistant", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        using var stillConfigurable = await client.GetAsync("/api/platform/assistant/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, stillConfigurable.StatusCode);
    }

    [Fact]
    public async Task Business_use_only_member_receives_no_configuration_or_unpermitted_quick_queries()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var ownerClient = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "State permissions", Ct);
        await AssistantTestData.EnableAsync(factory, tenantId);
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, account.UserId, Ct);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var role = Role.Create("State use only", null, RoleScope.Organization, null, [Permissions.Assistant.Use]).Value;
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.Add(role);
                db.Add(RoleAssignment.Assign(memberId, role.Id, role.Scope, null, null).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/organization/assistant/notice");

        using var state = await ReadAsync(client, "/api/organization/assistant");
        Assert.False(state.RootElement.GetProperty("canConfigure").GetBoolean());
        Assert.True(state.RootElement.GetProperty("noticeAccepted").GetBoolean());
        Assert.Empty(QuickKeys(state.RootElement));
        using var settings = await client.GetAsync("/api/organization/assistant/settings", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, settings.StatusCode);
    }

    [Fact]
    public async Task State_requires_authentication_the_matching_access_and_a_live_feature_grant()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = AssistantTestData.Client(factory);
        foreach (var path in new[] { "/api/me/assistant", "/api/organization/assistant", "/api/platform/assistant" })
        {
            using var anonymous = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        using var disabled = await client.GetAsync("/api/me/assistant", Ct);
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        using var wrongBusiness = await client.GetAsync("/api/organization/assistant", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongBusiness.StatusCode);
        using var wrongPlatform = await client.GetAsync("/api/platform/assistant", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongPlatform.StatusCode);

        var tenantId = await OrganizationJourney.StageAsync(factory, account, "State gates", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        using var noGrant = await client.GetAsync("/api/organization/assistant", Ct);
        Assert.Equal(HttpStatusCode.NotFound, noGrant.StatusCode);
        await AssistantTestData.EnableAsync(factory, tenantId);
        using var enabled = await ReadAsync(client, "/api/organization/assistant");
        Assert.False(enabled.RootElement.GetProperty("noticeAccepted").GetBoolean());
        using var wrongConsumer = await client.GetAsync("/api/me/assistant", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongConsumer.StatusCode);
    }

    [Fact]
    public async Task State_models_follow_the_live_allowlist_capabilities_effort_and_active_status_with_no_key()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Assistant.Use], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var compatible = Model("State eligible");
        var highOnly = Model("State high only", low: false);
        var noTools = Model("State no tools", tools: false);
        var insufficient = Model("State insufficient input", maxInput: 51000);
        var inactive = Model("State inactive", active: false);
        var unlisted = Model("State unlisted");
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.AddRange(compatible, highOnly, noTools, insufficient, inactive, unlisted);
                var settings = await db.Set<AssistantPlatformSettings>().SingleAsync(ct);
                db.Entry(settings).Property(row => row.Effort).CurrentValue = "Low";
                db.Entry(settings).Property(row => row.AllowedModelIds).CurrentValue =
                    [compatible.Id, highOnly.Id, noTools.Id, insufficient.Id, inactive.Id];
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(
                    AuditActorKind.System, Guid.Empty, "State catalog fixture",
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        using var state = await ReadAsync(client, "/api/platform/assistant");
        var model = Assert.Single(state.RootElement.GetProperty("models").EnumerateArray());
        Assert.Equal(compatible.Id, model.GetProperty("id").GetGuid());
        Assert.Equal("State eligible", model.GetProperty("name").GetString());
        Assert.False(state.RootElement.GetProperty("configured").GetBoolean());
        Assert.False(state.RootElement.GetProperty("noticeAccepted").GetBoolean());

        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var settings = await db.Set<AssistantPlatformSettings>().SingleAsync(ct);
                db.Entry(settings).Property(row => row.AllowedModelIds).CurrentValue = [unlisted.Id];
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(
                    AuditActorKind.System, Guid.Empty, "State live allowlist fixture",
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var changed = await ReadAsync(client, "/api/platform/assistant");
        Assert.Equal(unlisted.Id, Assert.Single(changed.RootElement.GetProperty("models").EnumerateArray()).GetProperty("id").GetGuid());
    }

    private static AssistantModelPrice Model(string name, bool low = true, bool tools = true, int maxInput = 100000, bool active = true)
    {
        var price = new Money(1m, CurrencyCode.Create("USD").Value);
        var id = Guid.NewGuid().ToString("N");
        var model = AssistantModelPrice.Create("Anthropic", id, name, price, price);
        model.Update("Anthropic", id, name, price, price, active);
        model.SetTechnical(AssistantModelTechnical.Create(true, tools, true, true, true,
            low, false, true, maxInput, 2048, price, price, price, null, null, null, null, null, null));
        return model;
    }

    private static async Task<JsonDocument> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    }

    private static string[] QuickKeys(JsonElement state) => state.GetProperty("quickQueries").EnumerateArray()
        .Select(item => item.GetProperty("key").GetString()!).ToArray();

    private static void AssertModels(JsonElement state)
    {
        var models = state.GetProperty("models").EnumerateArray().ToArray();
        Assert.Equal(2, models.Length);
        Assert.All(models, item =>
        {
            Assert.NotEqual(Guid.Empty, item.GetProperty("id").GetGuid());
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("name").GetString()));
            Assert.Equal(["id", "name"], item.EnumerateObject().Select(property => property.Name).Order());
        });
    }

    private sealed class StateProbe
    {
        internal bool ReadingState;
        internal int Transactions;
        internal int ClientCalls;
    }

    private sealed class ObservedUnitOfWork(IUnitOfWork inner, StateProbe probe) : IUnitOfWork
    {
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
            CommitPolicy policy, CancellationToken cancellationToken) where TResult : Result
        {
            if (probe.ReadingState) Interlocked.Increment(ref probe.Transactions);
            return inner.ExecuteInTransactionAsync(work, policy, cancellationToken);
        }
    }

    private sealed class ObservedClients(StateProbe probe) : IChatClientFactory
    {
        public Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref probe.ClientCalls);
            return Task.FromResult(Result.Failure<IChatClient>(ContractErrors.NotImplemented));
        }

        public Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref probe.ClientCalls);
            return Task.FromResult(Result.Failure(ContractErrors.NotImplemented));
        }
    }
}
