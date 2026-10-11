using System.Data.Common;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantStateFinanceReaderTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);
    private static FakeTimeProvider Clock(DateTime nowUtc) => new(new DateTimeOffset(nowUtc));

    [Fact]
    public async Task Concurrent_completion_does_not_mix_an_old_budget_with_new_monthly_cost()
    {
        var binding = await BusinessAsync();
        var first = await ReserveAsync(binding, NowUtc, AssistantMode.PlatformKey, .1m);
        await CompleteConservativelyAsync(first.Id, NowUtc.AddMinutes(1));
        var newer = await ReserveAsync(binding, NowUtc.AddMinutes(2), AssistantMode.PlatformKey, .2m);
        var barrier = new MonthSelectionBarrier();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(barrier))));
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
        var db = services.GetRequiredService<ApplicationDbContext>();
        barrier.Arm(db.ContextId.InstanceId);
        var reader = services.GetRequiredService<IAssistantFinanceReader>();
        var reading = reader.FindMonthCostAsync(first.BudgetMonthId!.Value, AssistantKeyFunding.PlatformKey, Ct);
        try
        {
            // The first SELECT has its PostgreSQL snapshot; another real connection completes the newer admission.
            await barrier.Selected.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            await CompleteConservativelyAsync(newer.Id, NowUtc.AddMinutes(3));
        }
        finally { barrier.Release.TrySetResult(); }
        var row = await reading.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        Assert.Equal(Usd(.1m), row.MonthlyBudget);
        Assert.Equal(Usd(.05632m), row.UsedCost);
        Assert.True(row.UsedCost!.Amount < row.MonthlyBudget!.Amount,
            "An admission completed after the query snapshot must not fabricate a monthly limit.");
        var after = await reader.FindMonthCostAsync(first.BudgetMonthId.Value, AssistantKeyFunding.PlatformKey, Ct);
        Assert.Equal(Usd(.2m), after.MonthlyBudget);
        Assert.Equal(Usd(.11264m), after.UsedCost);
        Assert.Empty(db.ChangeTracker.Entries());

        Task CompleteConservativelyAsync(Guid operationId, DateTime nowUtc) => WriteAsync(binding.ScopeTenantId, async writeServices =>
        {
            var writer = new AssistantUsageWriter(writeServices.GetRequiredService<IAssistantFinanceRepository>(), Clock(nowUtc));
            for (var sequence = 1; sequence <= 5; sequence++)
                Assert.True((await writer.BeginCallAsync(binding, operationId, sequence, false, Ct)).IsSuccess);
            return await writer.CompleteAsync(binding, operationId, Ct);
        });
    }

    [Fact]
    public async Task Daily_projection_shares_platform_account_day_and_isolates_own_quota_by_current_organization()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var first = await OrganizationJourney.StageAsync(factory, account, "State finance first", Ct);
        var second = await OrganizationJourney.StageAsync(factory, account, "State finance second", Ct);
        var day = DateOnly.FromDateTime(NowUtc);
        var platform = AssistantPlatformKeyDailyQuota.Create(account.UserId, day);
        for (var index = 0; index < 39; index++) { platform.Reserve(); platform.Consume(); }
        platform.Reserve();
        var ownFirst = AssistantOwnKeyDailyQuota.Create(account.UserId, day);
        ownFirst.Reserve(); ownFirst.Consume(); ownFirst.Reserve();
        var ownSecond = AssistantOwnKeyDailyQuota.Create(account.UserId, day);
        ownSecond.Reserve(); ownSecond.Reserve();
        await WriteAsync(first, services =>
        {
            services.GetRequiredService<ApplicationDbContext>().AddRange(platform, ownFirst);
            return Task.FromResult(Result.Success());
        });
        await WriteAsync(second, services =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(ownSecond);
            return Task.FromResult(Result.Success());
        });

        foreach (var organizationId in new Guid?[] { null, first, second })
        {
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            using var tenant = organizationId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
            var reader = services.GetRequiredService<IAssistantFinanceReader>();
            var daily = await reader.FindDailyQuotaAsync(account.UserId, AssistantMode.PlatformKey, day, Ct);
            Assert.Equal(new AssistantDailyQuotaUsageRow(39, 1), daily);
            Assert.Equal(10, AssistantLimitGuard.Describe(daily.ConsumedQueries, daily.ReservedQueries, 50, day).Remaining);
            Assert.Equal(new AssistantDailyQuotaUsageRow(0, 0), await reader.FindDailyQuotaAsync(account.UserId, AssistantMode.PlatformKey, day.AddDays(1), Ct));
            Assert.Equal(new AssistantDailyQuotaUsageRow(0, 0), await reader.FindDailyQuotaAsync(Guid.CreateVersion7(), AssistantMode.PlatformKey, day, Ct));
            if (organizationId is null)
                await Assert.ThrowsAsync<InvalidOperationException>(() => reader.FindDailyQuotaAsync(account.UserId, AssistantMode.OwnKey, day, Ct));
            else
                Assert.Equal(organizationId == first ? new(1, 1) : new AssistantDailyQuotaUsageRow(0, 2),
                    await reader.FindDailyQuotaAsync(account.UserId, AssistantMode.OwnKey, day, Ct));
            Assert.Empty(services.GetRequiredService<ApplicationDbContext>().ChangeTracker.Entries());
        }
    }

    [Fact]
    public async Task Monthly_projection_uses_completed_historical_cost_and_latest_admission_snapshot_of_the_same_funding()
    {
        var binding = await BusinessAsync();
        var other = await BusinessAsync();
        var first = await ReserveAsync(binding, NowUtc, AssistantMode.PlatformKey, 100);
        var second = await ReserveAsync(binding, NowUtc.AddMinutes(1), AssistantMode.PlatformKey, 200);
        var own = await ReserveAsync(binding, NowUtc.AddMinutes(2), AssistantMode.OwnKey, 500);
        await CompleteAsync(binding, second.Id, NowUtc.AddMinutes(3));
        await CompleteAsync(binding, own.Id, NowUtc.AddMinutes(4));
        // Closing the earlier admission later must not replace the newer admission's budget snapshot.
        await CompleteAsync(binding, first.Id, NowUtc.AddMinutes(5));
        var deleted = await ReserveAsync(binding, NowUtc.AddMinutes(6), AssistantMode.PlatformKey, 300);
        await WriteAsync(binding.ScopeTenantId, async services =>
        {
            var writer = new AssistantUsageWriter(services.GetRequiredService<IAssistantFinanceRepository>(), Clock(NowUtc.AddMinutes(7)));
            Assert.True((await writer.BeginCallAsync(binding, deleted.Id, 1, false, Ct)).IsSuccess);
            var operation = await services.GetRequiredService<IAssistantFinanceRepository>().GetOperationAsync(binding, deleted.Id, Ct);
            operation!.MarkContentDeleted();
            return await writer.CompleteAsync(binding, deleted.Id, Ct);
        });
        await ReserveAsync(binding, NowUtc.AddMinutes(8), AssistantMode.PlatformKey, 9999);
        var invoked = await ReserveAsync(binding, NowUtc.AddMinutes(9), AssistantMode.PlatformKey, 8888);
        await WriteAsync(binding.ScopeTenantId, async services =>
        {
            var writer = new AssistantUsageWriter(services.GetRequiredService<IAssistantFinanceRepository>(), Clock(NowUtc.AddMinutes(10)));
            Assert.True((await writer.BeginCallAsync(binding, invoked.Id, 1, false, Ct)).IsSuccess);
            return Result.Success();
        });
        var foreign = await ReserveAsync(other, NowUtc, AssistantMode.PlatformKey, 9000);
        await CompleteAsync(other, foreign.Id, NowUtc.AddMinutes(1));

        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
            var reader = services.GetRequiredService<IAssistantFinanceReader>();
            var platform = await reader.FindMonthCostAsync(first.BudgetMonthId!.Value, AssistantKeyFunding.PlatformKey, Ct);
            Assert.Equal(Usd(300), platform.MonthlyBudget);
            Assert.Equal(Usd(.011564m), platform.UsedCost);
            var ownCost = await reader.FindMonthCostAsync(first.BudgetMonthId.Value, AssistantKeyFunding.OwnKey, Ct);
            Assert.Equal(Usd(500), ownCost.MonthlyBudget);
            Assert.Equal(Usd(.00015m), ownCost.UsedCost);
            Assert.Equal(new AssistantMonthCostRow(null, null), await reader.FindMonthCostAsync(foreign.BudgetMonthId!.Value, AssistantKeyFunding.PlatformKey, Ct));
            Assert.Empty(services.GetRequiredService<ApplicationDbContext>().ChangeTracker.Entries());
            Assert.False(await services.GetRequiredService<ApplicationDbContext>().Set<AssistantUsageFact>()
                .AsNoTracking().AnyAsync(row => row.OperationId == deleted.Id, Ct));
        }
        using (var scope = factory.Services.CreateScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.FindMonthCostAsync(first.BudgetMonthId!.Value, AssistantKeyFunding.PlatformKey, Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.FindLatestMonthAsync(NowUtc, Ct));
        }
    }

    [Fact]
    public async Task Latest_month_preserves_open_transition_and_returns_expired_anchor_without_creating_future_periods()
    {
        var binding = await BusinessAsync();
        var startUtc = new DateTime(2026, 10, 1, 4, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc);
        var nextEndUtc = new DateTime(2026, 12, 1, 3, 0, 0, DateTimeKind.Utc);
        var original = AssistantBudgetMonth.Create(binding.ScopeTenantId!.Value, new(2026, 10, 1), "America/New_York", startUtc, endUtc);
        var next = AssistantBudgetMonth.Create(binding.ScopeTenantId.Value, new(2026, 11, 1), "America/Argentina/Buenos_Aires", endUtc, nextEndUtc);
        await WriteAsync(binding.ScopeTenantId, services =>
        {
            services.GetRequiredService<ApplicationDbContext>().AddRange(original, next);
            return Task.FromResult(Result.Success());
        });
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId.Value);
        var reader = services.GetRequiredService<IAssistantFinanceReader>();
        Assert.Null(await reader.FindLatestMonthAsync(startUtc.AddTicks(-1), Ct));
        var opened = await reader.FindLatestMonthAsync(endUtc.AddTicks(-1), Ct);
        Assert.Equal(new AssistantBudgetMonthRow(original.Id, original.Month, original.TimeZoneId, startUtc, endUtc), opened);
        Assert.Equal(next.Id, (await reader.FindLatestMonthAsync(endUtc, Ct))!.Id);
        Assert.Equal(next.Id, (await reader.FindLatestMonthAsync(nextEndUtc.AddMonths(4), Ct))!.Id);
        Assert.Equal(new AssistantMonthCostRow(null, null), await reader.FindMonthCostAsync(next.Id, AssistantKeyFunding.PlatformKey, Ct));
        Assert.Empty(services.GetRequiredService<ApplicationDbContext>().ChangeTracker.Entries());
        Assert.Equal(2, await services.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetMonth>()
            .AsNoTracking().CountAsync(row => row.OrganizationId == binding.ScopeTenantId, Ct));
    }

    private async Task<AssistantFinancialBinding> BusinessAsync()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var id = await OrganizationJourney.StageAsync(factory, account, "State finance organization", Ct);
        return new(account.UserId, Access.Business, id);
    }

    private async Task<AssistantFinancialOperation> ReserveAsync(AssistantFinancialBinding binding, DateTime nowUtc, AssistantMode mode, decimal budget)
    {
        AssistantFinancialOperation? operation = null;
        await WriteAsync(binding.ScopeTenantId, async services =>
        {
            var price = AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "state-fixture-haiku", "historical-v1",
                Usd(.1m), Usd(.5m), Usd(.125m), Usd(.2m), Usd(.01m), null, null, null, null, null, null);
            var guard = new AssistantLimitGuard(services.GetRequiredService<IAssistantFinanceRepository>(), services.GetRequiredService<ITimeZoneService>(), Clock(nowUtc));
            var admitted = await guard.ReserveAsync(new(Guid.CreateVersion7(), binding, mode, 50, Usd(budget), "UTC", price, null), Ct);
            Assert.True(admitted.IsSuccess, admitted.Error.Code);
            operation = admitted.Value;
            return Result.Success();
        });
        return operation!;
    }

    private Task CompleteAsync(AssistantFinancialBinding binding, Guid operationId, DateTime nowUtc) =>
        WriteAsync(binding.ScopeTenantId, async services =>
        {
            var writer = new AssistantUsageWriter(services.GetRequiredService<IAssistantFinanceRepository>(), Clock(nowUtc));
            Assert.True((await writer.BeginCallAsync(binding, operationId, 1, false, Ct)).IsSuccess);
            Assert.True((await writer.RecordUsageAsync(binding, operationId, 1, new(1000, 100, 0, 0, 0, 0, 30, true), Ct)).IsSuccess);
            return await writer.CompleteAsync(binding, operationId, Ct);
        });

    private async Task WriteAsync(Guid? organizationId, Func<IServiceProvider, Task<Result>> work)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = organizationId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => work(services), CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.Error.Code);
    }

    private sealed class MonthSelectionBarrier : DbCommandInterceptor
    {
        private Guid? contextId;
        private int armed;
        internal TaskCompletionSource Selected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Arm(Guid readerContextId) { contextId = readerContextId; armed = 1; }
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
