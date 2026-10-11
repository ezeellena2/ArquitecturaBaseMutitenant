using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantUsageAuthorizationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Month = "2026-10";

    [Fact]
    public async Task Business_configure_only_reads_empty_usage_without_use_notice_key_or_eligible_model()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var probe = new UsageProbe();
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Assistant:Platform:ApiKey", "");
            builder.ConfigureTestServices(services => ObserveReads(services, probe));
        });
        using var ownerClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(host, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Usage configure only", Ct);
        await AssistantTestData.EnableAsync(factory, tenantId);
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, account.UserId, Ct);
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var role = Role.Create("Usage configure only", null, RoleScope.Organization, null,
                    [Permissions.Assistant.Configure]).Value;
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.Add(role);
                db.Add(RoleAssignment.Assign(memberId, role.Id, role.Scope, null, null).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        await RemoveChatConfigurationAsync(host.Services);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);

        using var response = await ReadUsageAsync(client,
            $"/api/organization/assistant/usage?month={Month}&groupBy=day&page=1&pageSize=20", probe);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var usage = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        AssertEmptyPage(usage.RootElement);
        Assert.Equal(JsonValueKind.Null, usage.RootElement.GetProperty("platformKeyCost").ValueKind);
        Assert.Equal(JsonValueKind.Null, usage.RootElement.GetProperty("organizationCount").ValueKind);
        await AssertReadOnlyAsync(host.Services, account.UserId, probe);
    }

    [Fact]
    public async Task Platform_read_only_reads_empty_usage_with_chat_off_without_use_notice_key_or_eligible_model()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var probe = new UsageProbe();
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FeatureManagement:assistant", "false");
            builder.UseSetting("Assistant:Platform:ApiKey", "");
            builder.ConfigureTestServices(services => ObserveReads(services, probe));
        });
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.Assistant.Read], Ct);
        await RemoveChatConfigurationAsync(host.Services);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);

        using var response = await ReadUsageAsync(client,
            $"/api/platform/assistant/usage?month={Month}&page=1&pageSize=20", probe);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var usage = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        AssertEmptyPage(usage.RootElement);
        AssertZeroUsd(usage.RootElement.GetProperty("platformKeyCost"));
        Assert.Equal(0, usage.RootElement.GetProperty("organizationCount").GetInt32());
        await AssertReadOnlyAsync(host.Services, account.UserId, probe);
    }

    [Fact]
    public async Task Usage_rejects_invalid_queries_and_reads_representable_edge_months_without_overflow()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, account, "Usage validation", Ct);
        await AssistantTestData.EnableAsync(factory, tenantId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        foreach (var query in new[] { "", "?month=2026-13", "?month=2026-10&page=0", "?month=2026-10&page=2147483647", "?month=2026-10&pageSize=15" })
        {
            using var response = await client.GetAsync("/api/organization/assistant/usage" + query, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            Assert.Equal("Validation.Failed", problem.RootElement.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        }
        using var operatorClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var platform = await AccountJourney.RegisterAsync(factory, operatorClient, Ct);
        await PlatformOperators.GrantAsync(factory.Services, platform.UserId, [PlatformPermissions.Assistant.Read], Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        using var invalidPlatform = await operatorClient.GetAsync("/api/platform/assistant/usage?month=2026-10&page=2147483647", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPlatform.StatusCode);
        foreach (var month in new[] { "0001-01", "9999-12" })
        {
            using var business = await client.GetAsync("/api/organization/assistant/usage?month=" + month, Ct);
            using var global = await operatorClient.GetAsync("/api/platform/assistant/usage?month=" + month, Ct);
            Assert.Equal(HttpStatusCode.OK, business.StatusCode);
            Assert.Equal(HttpStatusCode.OK, global.StatusCode);
        }
    }

    private static void ObserveReads(IServiceCollection services, UsageProbe probe)
    {
        var original = services.Single(row => row.ServiceType == typeof(IUnitOfWork));
        services.Replace(ServiceDescriptor.Scoped<IUnitOfWork>(provider => new ObservedUnitOfWork(
            (IUnitOfWork)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!), probe)));
        services.RemoveAll<IChatClientFactory>();
        services.AddScoped<IChatClientFactory>(_ => new ObservedClients(probe));
    }

    private static async Task RemoveChatConfigurationAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var settings = await db.Set<AssistantPlatformSettings>().SingleAsync(ct);
            settings.ReplaceKey(null, null);
            settings.Update(Guid.Empty, [], settings.Effort, settings.DailyMessageLimit,
                settings.MonthlyOrganizationBudget, false, settings.RetentionDays);
            foreach (var model in await db.Set<AssistantModelPrice>().ToListAsync(ct))
                model.Update(model.Provider, model.ProviderModelId, model.DisplayName,
                    model.InputPrice, model.OutputPrice, false);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(
                AuditActorKind.System, Guid.Empty, "Usage without chat configuration fixture",
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<HttpResponseMessage> ReadUsageAsync(HttpClient client, string path, UsageProbe probe)
    {
        probe.ReadingUsage = true;
        try { return await client.GetAsync(path, Ct); }
        finally { probe.ReadingUsage = false; }
    }

    private static void AssertEmptyPage(JsonElement usage)
    {
        Assert.Equal(Month, usage.GetProperty("month").GetString());
        AssertZeroUsd(usage.GetProperty("cost"));
        Assert.Equal(0, usage.GetProperty("totalQueries").GetInt32());
        Assert.Equal(0, usage.GetProperty("peopleCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("budget").ValueKind);
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("budgetProgress").ValueKind);
        var rows = usage.GetProperty("rows");
        Assert.Empty(rows.GetProperty("items").EnumerateArray());
        Assert.Equal(1, rows.GetProperty("page").GetInt32());
        Assert.Equal(20, rows.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, rows.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, rows.GetProperty("totalPages").GetInt32());
        Assert.False(rows.GetProperty("hasPrevious").GetBoolean());
        Assert.False(rows.GetProperty("hasNext").GetBoolean());
    }

    private static void AssertZeroUsd(JsonElement money)
    {
        Assert.Equal(0m, money.GetProperty("amount").GetDecimal());
        Assert.Equal("USD", money.GetProperty("currency").GetString());
    }

    private static async Task AssertReadOnlyAsync(IServiceProvider provider, Guid ownerUserId, UsageProbe probe)
    {
        Assert.Equal(0, probe.Transactions);
        Assert.Equal(0, probe.ClientCalls);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<AssistantNoticeAcceptance>().AnyAsync(row => row.OwnerUserId == ownerUserId, Ct));
        Assert.False(await db.Set<AssistantBudgetMonth>().AnyAsync(Ct));
        Assert.False(await db.Set<AssistantFinancialOperation>().AnyAsync(Ct));
    }

    private sealed class UsageProbe
    {
        internal bool ReadingUsage;
        internal int Transactions;
        internal int ClientCalls;
    }

    private sealed class ObservedUnitOfWork(IUnitOfWork inner, UsageProbe probe) : IUnitOfWork
    {
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
            CommitPolicy policy, CancellationToken cancellationToken) where TResult : Result
        {
            if (probe.ReadingUsage) Interlocked.Increment(ref probe.Transactions);
            return inner.ExecuteInTransactionAsync(work, policy, cancellationToken);
        }
    }

    private sealed class ObservedClients(UsageProbe probe) : IChatClientFactory
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
