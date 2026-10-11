using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantStateBudgetTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private const string BusinessPath = "/api/organization/assistant";
    private static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);

    [Theory]
    [InlineData(AssistantMode.PlatformKey)]
    [InlineData(AssistantMode.OwnKey)]
    public async Task Monthly_progress_preserves_decimal_thresholds_and_unclipped_over_budget_cost(AssistantMode mode)
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var host = Host(factory, new(new DateTimeOffset(NowUtc)));
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await BusinessAsync(factory, client, account);
        await PlatformAsync(host, 99, true);
        await OrganizationAsync(host, tenant, mode, Usd(99), true);
        var month = await MonthAsync(host, tenant);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, BusinessPath + "/notice");
        var funding = mode == AssistantMode.OwnKey ? AssistantKeyFunding.OwnKey : AssistantKeyFunding.PlatformKey;
        var costs = new[] { 0m, 7.9m, 8m, 10m, 12m };
        var statuses = new[] { "WithinLimit", "WithinLimit", "NearLimit", "LimitReached", "LimitReached" };
        decimal previous = 0;
        for (var index = 0; index < costs.Length; index++)
        {
            await CostAsync(host, account.UserId, month, funding, 10, costs[index] - previous, index);
            using var state = await ReadAsync(client, BusinessPath);
            Assert.True(state.RootElement.GetProperty("configured").GetBoolean());
            AssertMonthly(state.RootElement, 10, costs[index], costs[index] / 10, statuses[index]);
            Assert.NotEmpty(state.RootElement.GetProperty("quickQueries").EnumerateArray());
            previous = costs[index];
        }
    }

    [Fact]
    public async Task Removing_a_key_and_changing_live_budget_preserves_the_recorded_month_and_deleted_content_cost()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var host = Host(factory, new(new DateTimeOffset(NowUtc)));
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await BusinessAsync(factory, client, account);
        await PlatformAsync(host, 10, true);
        var month = await MonthAsync(host, tenant);
        await CostAsync(host, account.UserId, month, AssistantKeyFunding.PlatformKey, 10, 8, 0, deleted: true);
        using (var configured = await ReadAsync(client, BusinessPath))
        {
            Assert.True(configured.RootElement.GetProperty("configured").GetBoolean());
            AssertMonthly(configured.RootElement, 10, 8, .8m, "NearLimit");
        }
        await PlatformAsync(host, 100, false);
        using var cleared = await ReadAsync(client, BusinessPath);
        Assert.False(cleared.RootElement.GetProperty("configured").GetBoolean());
        Assert.True(cleared.RootElement.GetProperty("canConfigure").GetBoolean());
        AssertMonthly(cleared.RootElement, 10, 8, .8m, "NearLimit");
        using var scope = host.Services.CreateScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<AssistantUsageFact>().AsNoTracking().ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Switching_funding_selects_its_own_snapshot_and_ignores_other_organizations_and_uncompleted_calls()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var host = Host(factory, new(new DateTimeOffset(NowUtc)));
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var first = await BusinessAsync(factory, client, account);
        var second = await BusinessAsync(factory, client, account);
        await PlatformAsync(host, 100, true);
        var month = await MonthAsync(host, first);
        var foreignMonth = await MonthAsync(host, second);
        await CostAsync(host, account.UserId, month, AssistantKeyFunding.PlatformKey, 10, 8, 0);
        await CostAsync(host, account.UserId, month, AssistantKeyFunding.OwnKey, 20, 2, 1);
        await CostAsync(host, account.UserId, foreignMonth, AssistantKeyFunding.PlatformKey, 100, 90, 0);
        await CostAsync(host, account.UserId, month, AssistantKeyFunding.PlatformKey, 500, 500, 2, state: AssistantFinancialState.Reserved);
        await CostAsync(host, account.UserId, month, AssistantKeyFunding.PlatformKey, 600, 600, 3, state: AssistantFinancialState.Invoked);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, first);
        using (var platform = await ReadAsync(client, BusinessPath))
            AssertMonthly(platform.RootElement, 10, 8, .8m, "NearLimit");
        await OrganizationAsync(host, first, AssistantMode.OwnKey, Usd(100), true);
        using (var own = await ReadAsync(client, BusinessPath))
        {
            Assert.True(own.RootElement.GetProperty("configured").GetBoolean());
            AssertMonthly(own.RootElement, 20, 2, .1m, "WithinLimit");
        }
        await OrganizationAsync(host, first, AssistantMode.PlatformKey, Usd(100), true);
        using var restored = await ReadAsync(client, BusinessPath);
        AssertMonthly(restored.RootElement, 10, 8, .8m, "NearLimit");
    }

    [Fact]
    public async Task Zero_budget_and_missing_applicable_budget_serialize_null_progress_without_creating_months()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var host = Host(factory, new(new DateTimeOffset(NowUtc)));
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await BusinessAsync(factory, client, account);
        await PlatformAsync(host, 0, true);
        using (var zero = await ReadAsync(client, BusinessPath))
        {
            AssertMoney(zero.RootElement, "monthlyBudget", 0);
            AssertMoney(zero.RootElement, "usedCost", 0);
            Assert.Equal(JsonValueKind.Null, zero.RootElement.GetProperty("monthlyBudgetProgress").ValueKind);
        }
        await OrganizationAsync(host, tenant, AssistantMode.OwnKey, null, true);
        using (var missing = await ReadAsync(client, BusinessPath))
        {
            Assert.True(missing.RootElement.GetProperty("configured").GetBoolean());
            Assert.Equal(JsonValueKind.Null, missing.RootElement.GetProperty("monthlyBudget").ValueKind);
            Assert.Equal(JsonValueKind.Null, missing.RootElement.GetProperty("usedCost").ValueKind);
            Assert.Equal(JsonValueKind.Null, missing.RootElement.GetProperty("monthlyBudgetProgress").ValueKind);
        }
        using var scope = host.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetMonth>()
            .AnyAsync(row => row.OrganizationId == tenant, Ct));
    }

    [Fact]
    public async Task Frozen_civil_period_expires_at_its_exact_boundary_and_get_does_not_materialize_a_new_month()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(new DateTime(2026, 11, 1, 3, 59, 0, DateTimeKind.Utc)));
        using var host = Host(factory, clock);
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await BusinessAsync(factory, client, account);
        await PlatformAsync(host, 30, true);
        var month = await MonthAsync(host, tenant);
        await CostAsync(host, account.UserId, month, AssistantKeyFunding.PlatformKey, 10, 8, 0);
        clock.SetUtcNow(new DateTimeOffset(month.EndUtc.AddTicks(-1)));
        using (var before = await ReadAsync(client, BusinessPath))
            AssertMonthly(before.RootElement, 10, 8, .8m, "NearLimit");
        clock.Advance(TimeSpan.FromTicks(1));
        using (var boundary = await ReadAsync(client, BusinessPath))
            AssertMonthly(boundary.RootElement, 30, 0, 0, "WithinLimit");
        await PlatformAsync(host, 30, false);
        using (var withoutKey = await ReadAsync(client, BusinessPath))
        {
            Assert.False(withoutKey.RootElement.GetProperty("configured").GetBoolean());
            Assert.Equal(JsonValueKind.Null, withoutKey.RootElement.GetProperty("monthlyBudget").ValueKind);
            Assert.Equal(JsonValueKind.Null, withoutKey.RootElement.GetProperty("usedCost").ValueKind);
            Assert.Equal(JsonValueKind.Null, withoutKey.RootElement.GetProperty("monthlyBudgetProgress").ValueKind);
        }
        using var scope = host.Services.CreateScope();
        var months = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetMonth>()
            .AsNoTracking().Where(row => row.OrganizationId == tenant).ToArrayAsync(Ct);
        var persisted = Assert.Single(months);
        Assert.Equal(month.Id, persisted.Id);
        Assert.Equal(month.EndUtc, persisted.EndUtc);
        Assert.Equal("America/New_York", persisted.TimeZoneId);
    }

    [Theory]
    [InlineData(38, 1, null)]
    [InlineData(39, 1, 10)]
    [InlineData(49, 1, 0)]
    public async Task Platform_daily_remainder_is_shared_between_personal_and_business_and_counts_reservations(
        int consumed, int reserved, int? remaining)
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var host = Host(factory, new(new DateTimeOffset(NowUtc)));
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var first = await BusinessAsync(factory, client, account);
        var second = await BusinessAsync(factory, client, account);
        await PlatformAsync(host, 100, false);
        var day = DateOnly.FromDateTime(NowUtc);
        var quota = AssistantPlatformKeyDailyQuota.Create(account.UserId, day);
        for (var index = 0; index < consumed; index++) { quota.Reserve(); quota.Consume(); }
        for (var index = 0; index < reserved; index++) quota.Reserve();
        await WriteAsync(host, null, services =>
        {
            var foreign = AssistantPlatformKeyDailyQuota.Create(Guid.CreateVersion7(), day);
            for (var index = 0; index < 50; index++) { foreign.Reserve(); foreign.Consume(); }
            services.GetRequiredService<ApplicationDbContext>().AddRange(quota, foreign);
            return Task.FromResult(Result.Success());
        });
        foreach (var tenant in new Guid?[] { null, first, second })
        {
            await AccountJourney.AuthorizeAsync(client, tenant is null ? "consumer" : "business", Ct, tenant);
            using var state = await ReadAsync(client, tenant is null ? "/api/me/assistant" : BusinessPath);
            AssertDaily(state.RootElement, remaining);
            if (tenant is null)
                Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("monthlyBudgetProgress").ValueKind);
        }
        await OrganizationAsync(host, first, AssistantMode.OwnKey, Usd(100), false);
        await OrganizationAsync(host, second, AssistantMode.OwnKey, Usd(100), false);
        var ownFirst = AssistantOwnKeyDailyQuota.Create(account.UserId, day);
        for (var index = 0; index < 39; index++) { ownFirst.Reserve(); ownFirst.Consume(); }
        ownFirst.Reserve();
        await WriteAsync(host, first, services =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(ownFirst);
            return Task.FromResult(Result.Success());
        });
        await AccountJourney.AuthorizeAsync(client, "business", Ct, first);
        using (var own = await ReadAsync(client, BusinessPath)) AssertDaily(own.RootElement, 10);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, second);
        using var independent = await ReadAsync(client, BusinessPath);
        AssertDaily(independent.RootElement, null);
    }

    private static WebApplicationFactory<Program> Host(ApiFactory factory, FakeTimeProvider clock) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));

    private static HttpClient Client(WebApplicationFactory<Program> host) =>
        host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<Guid> BusinessAsync(ApiFactory factory, HttpClient client, AccountJourney account)
    {
        var tenant = await OrganizationJourney.StageAsync(factory, account, "State budget organization", Ct);
        await AssistantTestData.EnableAsync(factory, tenant);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        return tenant;
    }

    private static Task PlatformAsync(WebApplicationFactory<Program> host, decimal budget, bool key) =>
        WriteAsync(host, null, async services =>
        {
            var settings = await services.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformSettings>().SingleAsync(Ct);
            settings.Update(settings.DefaultModelId, settings.AllowedModelIds, settings.Effort, 50, Usd(budget), true, 30);
            settings.ReplaceKey(key ? [1, 2, 3, 4] : null, key ? "test" : null);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(
                AuditActorKind.System, Guid.Empty, "State budget fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        });

    private static Task OrganizationAsync(WebApplicationFactory<Program> host, Guid tenant, AssistantMode mode, Money? budget, bool key) =>
        WriteAsync(host, tenant, async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var platform = await db.Set<AssistantPlatformSettings>().SingleAsync(Ct);
            var settings = await db.Set<AssistantOrganizationSettings>().SingleOrDefaultAsync(Ct);
            if (settings is null) { settings = AssistantOrganizationSettings.Create(); db.Add(settings); }
            settings.Update(mode.ToString(), platform.DefaultModelId, platform.Effort, 50, budget, 30);
            settings.ReplaceKey(key ? [1, 2, 3, 4] : null, key ? "test" : null);
            if (mode == AssistantMode.OwnKey)
            {
                var policy = await db.Set<AssistantOrganizationPolicy>().SingleOrDefaultAsync(row => row.OrganizationId == tenant, Ct);
                if (policy is null) { policy = AssistantOrganizationPolicy.Create(tenant, Usd(100)); db.Add(policy); }
                policy.SetAllowOwnKey(true);
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(
                    AuditActorKind.System, Guid.Empty, tenant, "State own funding fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            }
            return Result.Success();
        });

    private static async Task<AssistantBudgetMonth> MonthAsync(WebApplicationFactory<Program> host, Guid tenant)
    {
        var month = AssistantBudgetMonth.Create(tenant, new(2026, 10, 1), "America/New_York",
            new(2026, 10, 1, 4, 0, 0, DateTimeKind.Utc), new(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc));
        await WriteAsync(host, tenant, services =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(month);
            return Task.FromResult(Result.Success());
        });
        return month;
    }

    private static Task CostAsync(WebApplicationFactory<Program> host, Guid owner, AssistantBudgetMonth month,
        AssistantKeyFunding funding, decimal budget, decimal cost, int order, bool deleted = false,
        AssistantFinancialState state = AssistantFinancialState.Completed) =>
        WriteAsync(host, month.OrganizationId, services =>
        {
            // Historical ledger fixtures can represent reconciled overspend independently of current admission.
            var price = AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "state-budget-fixture", "historical-v1",
                Usd(cost * 1000), Usd(0), Usd(0), Usd(0), Usd(0), null, null, null, null, null, null);
            var operation = AssistantFinancialOperation.Create(Guid.CreateVersion7(), new(owner, Access.Business, month.OrganizationId),
                funding, DateOnly.FromDateTime(NowUtc), 50, month.Id, Usd(budget), Usd(25), price, null, NowUtc.AddMinutes(order));
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.Add(operation);
            if (state != AssistantFinancialState.Reserved)
            {
                operation.MarkInvoked();
                var call = AssistantFinancialCall.Create(operation.Id, 1, false, price, 1000, 0, Usd(cost));
                call.ReplaceUsage(1000, 0, 0, 0, 0, 0, null, false, Usd(cost));
                db.Add(call);
                if (state == AssistantFinancialState.Completed)
                {
                    call.FinalizeUsage();
                    operation.Complete(NowUtc.AddMinutes(order + 1));
                    if (deleted) operation.MarkContentDeleted();
                    else db.Add(AssistantUsageFact.Create(operation, call));
                }
            }
            return Task.FromResult(Result.Success());
        });

    private static async Task WriteAsync(WebApplicationFactory<Program> host, Guid? tenant, Func<IServiceProvider, Task<Result>> work)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenantScope = tenant is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => work(services), CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.Error.Code);
    }

    private static async Task<JsonDocument> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    }

    private static void AssertMonthly(JsonElement state, decimal budget, decimal cost, decimal fraction, string status)
    {
        AssertMoney(state, "monthlyBudget", budget);
        AssertMoney(state, "usedCost", cost);
        var progress = state.GetProperty("monthlyBudgetProgress");
        Assert.Equal(fraction, progress.GetProperty("fraction").GetDecimal());
        Assert.Equal(status, progress.GetProperty("status").GetString());
    }

    private static void AssertMoney(JsonElement state, string name, decimal amount)
    {
        var money = state.GetProperty(name);
        Assert.Equal(amount, money.GetProperty("amount").GetDecimal());
        Assert.Equal("USD", money.GetProperty("currency").GetString());
    }

    private static void AssertDaily(JsonElement state, int? remaining)
    {
        Assert.Equal(50, state.GetProperty("dailyLimit").GetInt32());
        var messages = state.GetProperty("remainingMessages");
        if (remaining is { } value) Assert.Equal(value, messages.GetInt32());
        else Assert.Equal(JsonValueKind.Null, messages.ValueKind);
        Assert.Equal("2026-10-11T00:00:00Z", state.GetProperty("dailyResetAtUtc").GetString());
    }
}
