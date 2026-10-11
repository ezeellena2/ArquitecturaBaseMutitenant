using System.Data.Common;
using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantUsageFinanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private const string Business = "/api/organization/assistant/usage?month=2026-10";
    private const string Platform = "/api/platform/assistant/usage?month=2026-10";
    private static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);

    [Fact]
    public async Task Business_mixed_funding_counts_people_once_and_keeps_long_tokens_titles_and_subcent_cost()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var host = Host(factory);
        using var client = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await OrganizationAsync(factory, client, owner, "Usage mixed");
        var month = await MonthAsync(host, tenant);
        await OperationAsync(host, owner.UserId, Access.Business, tenant, month, AssistantKeyFunding.PlatformKey,
            10m, NowUtc, [.000001m, .000002m, .000003m], 819_200_000L);
        await OperationAsync(host, owner.UserId, Access.Business, tenant, month, AssistantKeyFunding.OwnKey,
            20m, NowUtc.AddMinutes(1), [.000004m], 2_457_600_000L, deleted: true, readCache: true);
        await OperationAsync(host, owner.UserId, Access.Business, tenant, month, AssistantKeyFunding.PlatformKey,
            999m, NowUtc.AddMinutes(2), [], 1); // A reservation is not a consumed query or a row.
        var otherTenant = await OrganizationJourney.StageAsync(factory, owner, "Usage private other", Ct);
        var otherMonth = await MonthAsync(host, otherTenant);
        await OperationAsync(host, owner.UserId, Access.Business, otherTenant, otherMonth,
            AssistantKeyFunding.PlatformKey, 99m, NowUtc, [99m], 5);

        using var byDay = await ReadAsync(client, Business + "&groupBy=day&pageSize=10");
        var usage = byDay.RootElement;
        AssertMoney(usage, "cost", .000010m);
        AssertMoney(usage, "platformKeyCost", .000006m);
        Assert.Equal(2, usage.GetProperty("totalQueries").GetInt32());
        Assert.Equal(1, usage.GetProperty("peopleCount").GetInt32());
        Assert.Null(NullableMoney(usage, "budget"));
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("budgetProgress").ValueKind);
        var day = Assert.Single(Items(usage));
        Assert.Equal("day", day.GetProperty("kind").GetString());
        Assert.Equal("2026-10-10", day.GetProperty("day").GetString());
        Assert.Equal(1, day.GetProperty("peopleCount").GetInt32());
        Assert.Equal(4_915_200_000L, day.GetProperty("inputTokens").GetInt64());
        Assert.Equal(4_915_200_000L, day.GetProperty("outputTokens").GetInt64());
        Assert.Equal(2_457_600_000L, day.GetProperty("cacheWriteTokens").GetInt64());
        Assert.Equal(2_457_600_000L, day.GetProperty("cacheReadTokens").GetInt64());
        Assert.Equal(JsonValueKind.Null, day.GetProperty("dailyUsage").ValueKind);
        Assert.Equal(JsonValueKind.Null, day.GetProperty("fundingMode").ValueKind);
        Assert.Equal(JsonValueKind.Null, day.GetProperty("budgetProgress").ValueKind);
        using var byPerson = await ReadAsync(client, Business + "&groupBy=person");
        var person = Assert.Single(Items(byPerson.RootElement));
        Assert.Equal(2, person.GetProperty("queries").GetInt32());
        Assert.Equal(JsonValueKind.Null, person.GetProperty("peopleCount").ValueKind);
        AssertMoney(person, "cost", .000010m);
        Assert.DoesNotContain(owner.Email.Value, byPerson.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("conversationId", byPerson.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AssistantKeyFunding.PlatformKey)]
    [InlineData(AssistantKeyFunding.OwnKey)]
    public async Task Single_funding_uses_latest_completed_budget_snapshot_and_exact_unclipped_progress(AssistantKeyFunding funding)
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var host = Host(factory);
        using var client = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await OrganizationAsync(factory, client, owner, "Usage snapshots");
        var month = await MonthAsync(host, tenant);
        var costs = new[] { 0m, 7.9m, 8m, 10m, 12m };
        var statuses = new[] { "WithinLimit", "WithinLimit", "NearLimit", "LimitReached", "LimitReached" };
        decimal previous = 0;
        for (var index = 0; index < costs.Length; index++)
        {
            await OperationAsync(host, owner.UserId, Access.Business, tenant, month, funding,
                10m, NowUtc.AddMinutes(index), [costs[index] - previous], 100);
            using var response = await ReadAsync(client, Business + "&groupBy=person");
            AssertMoney(response.RootElement, "budget", 10m);
            AssertMoney(response.RootElement, "cost", costs[index]);
            AssertProgress(response.RootElement, costs[index] / 10m, statuses[index]);
            AssertMoney(Assert.Single(Items(response.RootElement)), "monthlyBudget", 10m);
            previous = costs[index];
        }
        await OperationAsync(host, owner.UserId, Access.Business, tenant, month, funding, 20m,
            NowUtc.AddMinutes(10), [0m], 1, deleted: true);
        await OperationAsync(host, owner.UserId, Access.Business, tenant, month, funding, 999m,
            NowUtc.AddMinutes(11), [], 1);
        using (var changed = await ReadAsync(client, Business))
        {
            AssertMoney(changed.RootElement, "budget", 20m);
            AssertProgress(changed.RootElement, .6m, "WithinLimit");
        }
        await OperationAsync(host, owner.UserId, Access.Business, tenant, month, funding, 0m,
            NowUtc.AddMinutes(12), [0m], 1);
        using var zero = await ReadAsync(client, Business);
        AssertMoney(zero.RootElement, "budget", 0m);
        Assert.Equal(JsonValueKind.Null, zero.RootElement.GetProperty("budgetProgress").ValueKind);
        AssertMoney(zero.RootElement, "cost", 12m);
    }

    [Fact]
    public async Task Logical_frozen_month_keeps_admission_day_and_late_completion_across_zone_transition()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(NowUtc));
        using var host = Host(factory, clock);
        using var client = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await OrganizationAsync(factory, client, owner, "Usage frozen Tokyo");
        var start = new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 31, 15, 0, 0, DateTimeKind.Utc);
        var month = await MonthAsync(host, tenant, "Asia/Tokyo", start, end);
        await OperationAsync(host, owner.UserId, Access.Business, tenant, month, AssistantKeyFunding.PlatformKey,
            10m, start, [3m], 3, completedUtc: end.AddDays(3));
        var november = AssistantBudgetMonth.Create(tenant, new(2026, 11, 1), "America/New_York", end,
            new(2026, 12, 1, 5, 0, 0, DateTimeKind.Utc));
        await WriteAsync(host, tenant, services => { services.GetRequiredService<ApplicationDbContext>().Add(november); return Task.FromResult(Result.Success()); });
        await OperationAsync(host, owner.UserId, Access.Business, tenant, november, AssistantKeyFunding.PlatformKey,
            20m, end, [7m], 7);
        clock.SetUtcNow(new DateTimeOffset(end));
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        using var october = await ReadAsync(client, Business + "&groupBy=day");
        AssertMoney(october.RootElement, "cost", 3m);
        Assert.Equal("2026-10-01", Assert.Single(Items(october.RootElement)).GetProperty("day").GetString());
        using var historicalPerson = await ReadAsync(client, Business + "&groupBy=person");
        Assert.Equal(JsonValueKind.Null, Assert.Single(Items(historicalPerson.RootElement)).GetProperty("dailyUsage").ValueKind);
        using var next = await ReadAsync(client, "/api/organization/assistant/usage?month=2026-11&groupBy=day");
        AssertMoney(next.RootElement, "cost", 7m);
        Assert.Equal(1, next.RootElement.GetProperty("totalQueries").GetInt32());
        using var empty = await ReadAsync(client, "/api/organization/assistant/usage?month=2026-12");
        Assert.Empty(Items(empty.RootElement));
        Assert.Null(NullableMoney(empty.RootElement, "budget"));
        using var operatorClient = Client(host);
        var account = await AccountJourney.RegisterAsync(host, operatorClient, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.Assistant.Read], Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        using var global = await ReadAsync(operatorClient, Platform);
        AssertMoney(global.RootElement, "cost", 3m);
        AssertMoney(Assert.Single(Items(global.RootElement)), "monthlyBudget", 10m);
    }

    [Theory]
    [InlineData(AssistantMode.PlatformKey, 49)]
    [InlineData(AssistantMode.OwnKey, 3)]
    public async Task Current_person_daily_usage_reads_account_utc_quota_and_reservations_only_affect_limit_flag(AssistantMode mode, int consumed)
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var host = Host(factory);
        using var client = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await OrganizationAsync(factory, client, owner, "Usage current daily");
        var month = await MonthAsync(host, tenant);
        await WriteAsync(host, tenant, services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var settings = AssistantOrganizationSettings.Create();
            settings.Update(mode.ToString(), Guid.Empty, "Low", mode == AssistantMode.OwnKey ? 4 : 50, Usd(10), 30);
            db.Add(settings);
            var day = DateOnly.FromDateTime(NowUtc);
            if (mode == AssistantMode.PlatformKey)
            {
                var quota = AssistantPlatformKeyDailyQuota.Create(owner.UserId, day);
                for (var index = 0; index < consumed; index++) { quota.Reserve(); quota.Consume(); }
                quota.Reserve(); db.Add(quota);
            }
            else
            {
                var quota = AssistantOwnKeyDailyQuota.Create(owner.UserId, day);
                for (var index = 0; index < consumed; index++) { quota.Reserve(); quota.Consume(); }
                quota.Reserve(); db.Add(quota);
            }
            return Task.FromResult(Result.Success());
        });
        await OperationAsync(host, owner.UserId, Access.Business, tenant, month,
            mode == AssistantMode.OwnKey ? AssistantKeyFunding.OwnKey : AssistantKeyFunding.PlatformKey,
            10m, NowUtc, [.000150m], 1000);
        using var response = await ReadAsync(client, Business + "&groupBy=person");
        var daily = Assert.Single(Items(response.RootElement)).GetProperty("dailyUsage");
        Assert.Equal("2026-10-10", daily.GetProperty("dayUtc").GetString());
        Assert.Equal("2026-10-11T00:00:00Z", daily.GetProperty("resetAtUtc").GetString());
        Assert.Equal(mode.ToString(), daily.GetProperty("scope").GetString());
        Assert.Equal(consumed, daily.GetProperty("queries").GetInt32());
        Assert.Equal(mode == AssistantMode.OwnKey ? 4 : 50, daily.GetProperty("limit").GetInt32());
        Assert.True(daily.GetProperty("limitReached").GetBoolean());
        Assert.Equal(1, response.RootElement.GetProperty("totalQueries").GetInt32());
    }

    [Fact]
    public async Task Platform_pages_funding_groups_but_totals_distinct_people_and_organizations_cover_the_whole_month()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var sql = new UsageSqlProbe();
        using var host = Host(factory, sql: sql);
        using var client = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var personal = await AssistantTestData.ActiveTenantAsync(client);
        Guid first = Guid.Empty;
        for (var index = 0; index < 10; index++)
        {
            var tenant = await OrganizationJourney.StageAsync(factory, owner, $"Usage global {index:D2}", Ct);
            var month = await MonthAsync(host, tenant);
            if (index == 0) first = tenant;
            await OperationAsync(host, owner.UserId, Access.Business, tenant, month,
                AssistantKeyFunding.PlatformKey, 25m, NowUtc, [2m], index == 0 ? 2_457_600_000L : 100);
            if (index == 0)
                await OperationAsync(host, owner.UserId, Access.Business, tenant, month, AssistantKeyFunding.OwnKey,
                    7m, NowUtc.AddMinutes(1), [7m], 2_457_600_000L, readCache: true);
        }
        await OperationAsync(host, owner.UserId, Access.Consumer, personal, null, AssistantKeyFunding.PlatformKey,
            null, NowUtc, [6m], 100);
        using var operatorClient = Client(host);
        var account = await AccountJourney.RegisterAsync(host, operatorClient, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.Assistant.Read], Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        await OperationAsync(host, account.UserId, Access.Platform, null, null, AssistantKeyFunding.PlatformKey,
            null, NowUtc, [4m], 100);
        using var warm = await ReadAsync(operatorClient, Platform + "&page=1&pageSize=10");
        sql.Arm();
        using var page1 = await ReadAsync(operatorClient, Platform + "&page=1&pageSize=10");
        var firstCommands = sql.Disarm();
        sql.Arm();
        using var page2 = await ReadAsync(operatorClient, Platform + "&page=2&pageSize=10");
        var secondCommands = sql.Disarm();
        Assert.Contains(firstCommands, command => command.Contains("AssistantFinancial", StringComparison.Ordinal)
            && command.Contains("LIMIT", StringComparison.Ordinal));
        Assert.Contains(secondCommands, command => command.Contains("AssistantFinancial", StringComparison.Ordinal)
            && command.Contains("OFFSET", StringComparison.Ordinal));
        Assert.InRange(firstCommands.Length - secondCommands.Length, -2, 2);
        foreach (var page in new[] { page1.RootElement, page2.RootElement })
        {
            AssertMoney(page, "cost", 37m);
            AssertMoney(page, "platformKeyCost", 30m);
            Assert.Equal(13, page.GetProperty("totalQueries").GetInt32());
            Assert.Equal(2, page.GetProperty("peopleCount").GetInt32());
            Assert.Equal(10, page.GetProperty("organizationCount").GetInt32());
            Assert.Equal(13, page.GetProperty("rows").GetProperty("totalCount").GetInt32());
            Assert.Null(NullableMoney(page, "budget"));
            Assert.Equal(JsonValueKind.Null, page.GetProperty("budgetProgress").ValueKind);
        }
        var all = Items(page1.RootElement).Concat(Items(page2.RootElement)).ToArray();
        Assert.Equal(13, all.Length);
        var fundingRows = all.Where(row => row.GetProperty("organizationId").ValueKind != JsonValueKind.Null
            && row.GetProperty("organizationId").GetGuid() == first).ToArray();
        Assert.Equal(2, fundingRows.Length);
        var pk = Assert.Single(fundingRows, row => row.GetProperty("fundingMode").GetString() == "PlatformKey");
        Assert.Equal(2_457_600_000L, pk.GetProperty("inputTokens").GetInt64());
        Assert.Equal(2_457_600_000L, pk.GetProperty("outputTokens").GetInt64());
        Assert.Equal(2_457_600_000L, pk.GetProperty("cacheWriteTokens").GetInt64());
        AssertMoney(pk, "monthlyBudget", 25m);
        AssertProgress(pk, .08m, "WithinLimit");
        var own = Assert.Single(fundingRows, row => row.GetProperty("fundingMode").GetString() == "OwnKey");
        Assert.Equal(2_457_600_000L, own.GetProperty("cacheReadTokens").GetInt64());
        AssertProgress(own, 1m, "LimitReached");
        foreach (var row in all)
        {
            Assert.Equal(JsonValueKind.Null, row.GetProperty("peopleCount").ValueKind);
            Assert.Equal(JsonValueKind.Null, row.GetProperty("dailyUsage").ValueKind);
            Assert.Equal(JsonValueKind.Null, row.GetProperty("personId").ValueKind);
        }
        foreach (var kind in new[] { "personal", "operator" })
        {
            var row = Assert.Single(all, row => row.GetProperty("kind").GetString() == kind);
            Assert.Null(NullableMoney(row, "monthlyBudget"));
            Assert.Equal(JsonValueKind.Null, row.GetProperty("budgetProgress").ValueKind);
        }
        using var beyond = await ReadAsync(operatorClient, Platform + "&page=3&pageSize=10");
        Assert.Empty(Items(beyond.RootElement));
        Assert.Equal(13, beyond.RootElement.GetProperty("rows").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Business_day_pages_keep_monthly_distinct_people_and_civil_day_counts_before_pagination()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var sql = new UsageSqlProbe();
        using var host = Host(factory, sql: sql);
        using var client = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await OrganizationAsync(factory, client, owner, "Usage days");
        using var secondClient = Client(host);
        var second = await AccountJourney.RegisterAsync(host, secondClient, Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenant, second.UserId, Ct);
        var month = await MonthAsync(host, tenant);
        for (var index = 0; index < 11; index++)
        {
            var day = new DateTime(2026, 10, index + 1, 12, 0, 0, DateTimeKind.Utc);
            await OperationAsync(host, owner.UserId, Access.Business, tenant, month,
                AssistantKeyFunding.PlatformKey, 10m, day, [.00015m], 1000);
            if (index < 2)
                await OperationAsync(host, second.UserId, Access.Business, tenant, month,
                    AssistantKeyFunding.PlatformKey, 10m, day.AddMinutes(1), [.00015m], 1000);
        }
        using var warm = await ReadAsync(client, Business + "&groupBy=day&pageSize=10");
        sql.Arm();
        using var first = await ReadAsync(client, Business + "&groupBy=day&page=1&pageSize=10");
        var firstSql = sql.Disarm();
        sql.Arm();
        using var next = await ReadAsync(client, Business + "&groupBy=day&page=2&pageSize=10");
        var nextSql = sql.Disarm();
        Assert.Contains(nextSql, command => command.Contains("AssistantFinancial", StringComparison.Ordinal)
            && command.Contains("OFFSET", StringComparison.Ordinal));
        Assert.InRange(firstSql.Length - nextSql.Length, -2, 2);
        foreach (var response in new[] { first.RootElement, next.RootElement })
        {
            AssertMoney(response, "cost", .00195m);
            Assert.Equal(13, response.GetProperty("totalQueries").GetInt32());
            Assert.Equal(2, response.GetProperty("peopleCount").GetInt32());
            Assert.Equal(11, response.GetProperty("rows").GetProperty("totalCount").GetInt32());
        }
        var all = Items(first.RootElement).Concat(Items(next.RootElement)).ToArray();
        Assert.Equal(11, all.Length);
        Assert.Equal(11, all.Select(row => row.GetProperty("day").GetString()).Distinct().Count());
        Assert.Equal(2, Assert.Single(all, row => row.GetProperty("day").GetString() == "2026-10-01").GetProperty("peopleCount").GetInt32());
        Assert.Equal(2, Assert.Single(all, row => row.GetProperty("day").GetString() == "2026-10-02").GetProperty("peopleCount").GetInt32());
        using var persons = await ReadAsync(client, Business + "&groupBy=person");
        Assert.Equal(new[] { 2, 11 }, Items(persons.RootElement).Select(row => row.GetProperty("queries").GetInt32()).Order().ToArray());
        using var repeated = await ReadAsync(client, Business + "&groupBy=day&page=1&pageSize=10");
        Assert.Equal(Items(first.RootElement).Select(row => row.GetProperty("day").GetString()),
            Items(repeated.RootElement).Select(row => row.GetProperty("day").GetString()));
    }

    [Fact]
    public async Task Concurrent_completion_keeps_summary_budget_and_cost_in_one_database_snapshot()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var barrier = new UsageSelectionBarrier();
        using var host = Host(factory, sql: barrier);
        using var client = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenantId = await OrganizationAsync(factory, client, owner, "Usage financial snapshot");
        var month = await MonthAsync(host, tenantId);
        await OperationAsync(host, owner.UserId, Access.Business, tenantId, month,
            AssistantKeyFunding.PlatformKey, 10m, NowUtc, [1m], 1000);
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        barrier.Arm(services.GetRequiredService<ApplicationDbContext>().ContextId.InstanceId);
        var reader = services.GetRequiredService<IAssistantUsageReader>();
        var reading = reader.ListOrganizationAsync(new(2026, 10, 1), AssistantUsageGrouping.Day, 1, 20, NowUtc, Ct);
        try
        {
            await barrier.Selected.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            await OperationAsync(host, owner.UserId, Access.Business, tenantId, month,
                AssistantKeyFunding.PlatformKey, 20m, NowUtc.AddMinutes(1), [2m], 1000);
        }
        finally { barrier.Release.TrySetResult(); }
        var snapshot = await reading.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        Assert.Equal(Usd(10m), snapshot.Budget);
        Assert.Equal(Usd(1m), snapshot.Cost);
        var after = await reader.ListOrganizationAsync(new(2026, 10, 1), AssistantUsageGrouping.Day, 1, 20, NowUtc, Ct);
        Assert.Equal(Usd(20m), after.Budget);
        Assert.Equal(Usd(3m), after.Cost);
        Assert.Empty(services.GetRequiredService<ApplicationDbContext>().ChangeTracker.Entries());
    }

    [Fact]
    public async Task Usage_does_not_restore_names_after_content_purge_or_an_expired_deletion_lease()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var sql = new UsageSqlProbe();
        using var host = Host(factory, sql: sql);
        using var client = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenantId = await OrganizationAsync(factory, client, owner, "Usage identity privacy");
        using var memberClient = Client(host);
        var member = await AccountJourney.RegisterAsync(host, memberClient, Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, member.UserId, Ct);
        await WriteAsync(host, null, async services =>
        {
            var users = services.GetRequiredService<ApplicationDbContext>().Users;
            Assert.True((await users.SingleAsync(user => user.Id == owner.UserId, Ct)).UpdateNames("Owner", "Visible").IsSuccess);
            Assert.True((await users.SingleAsync(user => user.Id == member.UserId, Ct)).UpdateNames("Member", "Visible").IsSuccess);
            return Result.Success();
        });
        var month = await MonthAsync(host, tenantId);
        foreach (var user in new[] { owner.UserId, member.UserId })
            await OperationAsync(host, user, Access.Business, tenantId, month, AssistantKeyFunding.PlatformKey,
                10m, NowUtc, [.000150m], 1000);
        using (var scope = host.Services.CreateScope())
        {
            sql.Arm();
            var names = await scope.ServiceProvider.GetRequiredService<IUserDisplayNameReader>().FindAsync([owner.UserId, member.UserId], Ct);
            var command = Assert.Single(sql.Disarm());
            Assert.Equal(2, names.Count);
            foreach (var column in new[] { "Email", "PhoneNumber", "PasswordHash", "SecurityStamp", "ConcurrencyStamp" })
                Assert.DoesNotContain(column, command, StringComparison.Ordinal);
        }
        using (var before = await ReadAsync(client, Business + "&groupBy=person"))
        {
            Assert.Contains("Owner Visible", before.RootElement.GetRawText(), StringComparison.Ordinal);
            Assert.Contains("Member Visible", before.RootElement.GetRawText(), StringComparison.Ordinal);
        }
        await WriteAsync(host, tenantId, async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var operation = await db.Set<AssistantFinancialOperation>().SingleAsync(row => row.UserId == owner.UserId, Ct);
            operation.MarkContentDeleted();
            var user = await db.Users.SingleAsync(row => row.Id == member.UserId, Ct);
            db.Entry(user).Property(value => value.DeletionLeaseId).CurrentValue = Guid.CreateVersion7();
            db.Entry(user).Property(value => value.DeletionLeaseExpiresAtUtc).CurrentValue = NowUtc.AddDays(-1);
            return Result.Success();
        });
        using var after = await ReadAsync(client, Business + "&groupBy=person");
        Assert.DoesNotContain("Owner Visible", after.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Member Visible", after.RootElement.GetRawText(), StringComparison.Ordinal);
        AssertMoney(after.RootElement, "cost", .000300m);
        Assert.Equal(2, after.RootElement.GetProperty("totalQueries").GetInt32());
        Assert.Equal(2, after.RootElement.GetProperty("peopleCount").GetInt32());
    }

    private static WebApplicationFactory<Program> Host(ApiFactory factory, FakeTimeProvider? clock = null, DbCommandInterceptor? sql = null) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock ?? new FakeTimeProvider(new DateTimeOffset(NowUtc))));
            if (sql is not null) services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(sql));
        }));
    private static HttpClient Client(WebApplicationFactory<Program> host) => host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task<Guid> OrganizationAsync(ApiFactory factory, HttpClient client, AccountJourney owner, string name)
    {
        var tenant = await OrganizationJourney.StageAsync(factory, owner, name, Ct);
        await AssistantTestData.EnableAsync(factory, tenant);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        return tenant;
    }
    private static async Task<AssistantBudgetMonth> MonthAsync(WebApplicationFactory<Program> host, Guid tenant,
        string zone = "UTC", DateTime? startUtc = null, DateTime? endUtc = null)
    {
        var month = AssistantBudgetMonth.Create(tenant, new(2026, 10, 1), zone,
            startUtc ?? new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), endUtc ?? new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        await WriteAsync(host, tenant, services => { services.GetRequiredService<ApplicationDbContext>().Add(month); return Task.FromResult(Result.Success()); });
        return month;
    }
    private static Task OperationAsync(WebApplicationFactory<Program> host, Guid user, Access access, Guid? tenant,
        AssistantBudgetMonth? month, AssistantKeyFunding funding, decimal? budget, DateTime admittedUtc,
        decimal[] costs, long tokens, bool deleted = false, DateTime? completedUtc = null, bool readCache = false) =>
        WriteAsync(host, tenant, services =>
        {
            var price = AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "usage-snapshot-fixture", "historical-price-v1",
                Usd(.1m), Usd(.5m), Usd(.125m), Usd(.2m), Usd(.01m), null, null, null, null, null, null);
            var operation = AssistantFinancialOperation.Create(Guid.CreateVersion7(), new(user, access, tenant), funding,
                DateOnly.FromDateTime(admittedUtc), 50, month?.Id, budget is { } value ? Usd(value) : null, Usd(.05m), price, null, admittedUtc);
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.Add(operation);
            if (costs.Length > 0)
            {
                operation.MarkInvoked();
                for (var index = 0; index < costs.Length; index++)
                {
                    var call = AssistantFinancialCall.Create(operation.Id, index + 1, index == costs.Length - 1 && index > 0,
                        AssistantPriceSnapshotBuilder.Copy(price), tokens, tokens, Usd(costs[index]));
                    call.ReplaceUsage(tokens, tokens, readCache ? tokens : 0, readCache ? 0 : tokens, 0, 0, null, false, Usd(costs[index]));
                    call.FinalizeUsage(); db.Add(call);
                    if (access != Access.Platform && !deleted) db.Add(AssistantUsageFact.Create(operation, call));
                }
                operation.Complete(completedUtc ?? admittedUtc.AddMinutes(1));
                if (deleted) operation.MarkContentDeleted();
            }
            return Task.FromResult(Result.Success());
        });
    private static async Task WriteAsync(WebApplicationFactory<Program> host, Guid? tenantId, Func<IServiceProvider, Task<Result>> work)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = tenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => work(services), CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.Error.Code);
    }
    private static async Task<JsonDocument> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    }
    private static JsonElement[] Items(JsonElement response) => response.GetProperty("rows").GetProperty("items").EnumerateArray().ToArray();
    private static decimal? NullableMoney(JsonElement response, string name) => response.GetProperty(name).ValueKind == JsonValueKind.Null ? null : response.GetProperty(name).GetProperty("amount").GetDecimal();
    private static void AssertMoney(JsonElement response, string name, decimal amount)
    {
        Assert.Equal(amount, response.GetProperty(name).GetProperty("amount").GetDecimal());
        Assert.Equal("USD", response.GetProperty(name).GetProperty("currency").GetString());
    }
    private static void AssertProgress(JsonElement response, decimal fraction, string status)
    {
        Assert.Equal(fraction, response.GetProperty("budgetProgress").GetProperty("fraction").GetDecimal());
        Assert.Equal(status, response.GetProperty("budgetProgress").GetProperty("status").GetString());
    }
    private sealed class UsageSqlProbe : DbCommandInterceptor
    {
        private readonly List<string> commands = [];
        private bool armed;
        internal void Arm() { commands.Clear(); armed = true; }
        internal string[] Disarm() { armed = false; return commands.ToArray(); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (armed) commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
    private sealed class UsageSelectionBarrier : DbCommandInterceptor
    {
        private Guid contextId;
        private int armed;
        internal TaskCompletionSource Selected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Arm(Guid id) { contextId = id; armed = 1; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ContextId.InstanceId == contextId
                && command.CommandText.Contains("AssistantFinancialOperations", StringComparison.Ordinal)
                && Interlocked.Exchange(ref armed, 0) == 1)
            {
                Selected.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
