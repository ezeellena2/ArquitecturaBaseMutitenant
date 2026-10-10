using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantFinanceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);
    private static AssistantPriceSnapshot Price() => AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "fixture-haiku", "price-v1",
        Usd(.1m), Usd(.5m), Usd(.125m), Usd(.2m), Usd(.01m), null, null, null, null, null, null);
    private static FakeTimeProvider Clock(DateTime nowUtc) => new(new DateTimeOffset(nowUtc));

    [Fact]
    public async Task Platform_fifty_is_shared_across_personal_two_organizations_and_operator_and_own_is_independent()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var personalId = await AssistantTestData.ActiveTenantAsync(client);
        var firstId = await OrganizationJourney.StageAsync(factory, account, "Finance shared first", Ct);
        var secondId = await OrganizationJourney.StageAsync(factory, account, "Finance shared second", Ct);
        var personal = new AssistantFinancialBinding(account.UserId, Access.Consumer, personalId);
        var first = new AssistantFinancialBinding(account.UserId, Access.Business, firstId);
        var second = new AssistantFinancialBinding(account.UserId, Access.Business, secondId);
        for (var i = 0; i < 40; i++) await AdmitAndCompleteAsync(factory, personal);
        using (var scope = factory.Services.CreateScope())
        {
            var quota = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformKeyDailyQuota>()
                .AsNoTracking().SingleAsync(row => row.UserId == account.UserId && row.DayUtc == DateOnly.FromDateTime(NowUtc), Ct);
            Assert.Equal(10, AssistantLimitGuard.Describe(quota.ConsumedQueries, quota.ReservedQueries, 50, quota.DayUtc).Remaining);
        }
        for (var i = 0; i < 10; i++) await AdmitAndCompleteAsync(factory, i % 2 == 0 ? first : second);
        var exhausted = await ReserveAsync(factory, second, Guid.CreateVersion7());
        Assert.Equal(AssistantErrors.LimitDailyReachedCode, exhausted.Error.Code);
        Assert.Equal(new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc), exhausted.Error.Metadata!["dailyResetAtUtc"]);
        // This technical assertion exercises the global key only; it does not grant a personal account operator access.
        var globalBinding = new AssistantFinancialBinding(account.UserId, Access.Platform, null);
        Assert.Equal(AssistantErrors.LimitDailyReachedCode, (await ReserveAsync(factory, globalBinding, Guid.CreateVersion7())).Error.Code);
        Assert.True((await ReserveAsync(factory, first, Guid.CreateVersion7(), mode: AssistantMode.OwnKey)).IsSuccess);
        Assert.True((await ReserveAsync(factory, second, Guid.CreateVersion7(), mode: AssistantMode.OwnKey)).IsSuccess);
        var nextDay = await ReserveAsync(factory, personal, Guid.CreateVersion7(), NowUtc.Date.AddDays(1), zone: "America/New_York");
        Assert.True(nextDay.IsSuccess);
        Assert.Null(nextDay.Value.MonthlyBudget); // Platform runtime carries a budget even for Consumer: binding discards it.
    }

    [Fact]
    public async Task Two_hosts_serialize_daily_quota_and_budget_without_overspending()
    {
        var binding = await BusinessAsync();
        using var replica = factory.WithWebHostBuilder(_ => { });
        _ = replica.Services;
        var attempts = await Task.WhenAll(Enumerable.Range(0, 51).Select(index =>
            ReserveAsync(index % 2 == 0 ? factory : replica, binding, Guid.CreateVersion7())));
        Assert.Equal(50, attempts.Count(row => row.IsSuccess));
        Assert.Equal(AssistantErrors.LimitDailyReachedCode, Assert.Single(attempts, row => row.IsFailure).Error.Code);
        foreach (var admitted in attempts.Where(row => row.IsSuccess)) await CompleteAsync(factory, binding, admitted.Value.Id, invoke: false);
        using var client = AssistantTestData.Client(factory);
        var another = await AccountJourney.RegisterAsync(factory, client, Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, binding.ScopeTenantId!.Value, another.UserId, Ct);
        var otherBinding = binding with { UserId = another.UserId };
        var budgetAttempts = await Task.WhenAll(ReserveAsync(factory, binding, Guid.CreateVersion7(), budget: .06m),
            ReserveAsync(replica, otherBinding, Guid.CreateVersion7(), budget: .06m));
        Assert.Single(budgetAttempts, row => row.IsSuccess);
        Assert.Equal(AssistantErrors.LimitMonthlyReachedCode, Assert.Single(budgetAttempts, row => row.IsFailure).Error.Code);
        var active = Assert.Single(budgetAttempts, row => row.IsSuccess).Value;
        Assert.Equal(.05632m, active.ReservedCost.Amount);
        Assert.True((await CompleteAsync(factory, active.IsBoundTo(binding) ? binding : otherBinding, active.Id, invoke: true)).IsSuccess);
        Assert.True((await ReserveAsync(replica, otherBinding, Guid.CreateVersion7(), budget: .06m)).IsSuccess);
    }

    [Fact]
    public async Task Before_invocation_releases_but_after_invocation_consumes_and_double_completion_does_not_duplicate()
    {
        var binding = await BusinessAsync();
        var before = (await ReserveAsync(factory, binding, Guid.CreateVersion7())).Value;
        await CompleteAsync(factory, binding, before.Id, invoke: false);
        var replay = await ReserveAsync(factory, binding, before.Id);
        Assert.Equal(AssistantFinancialState.Released, replay.Value.State);
        var cannotInvoke = await WriteAsync(factory, binding, async services =>
        {
            var result = await Writer(services).BeginCallAsync(binding, before.Id, 1, false, Ct);
            return result.IsSuccess ? Result.Success() : result.Error;
        });
        Assert.Equal(AssistantErrors.TurnInProgressCode, cannotInvoke.Error.Code);
        var after = (await ReserveAsync(factory, binding, Guid.CreateVersion7())).Value;
        await CompleteAsync(factory, binding, after.Id, invoke: true);
        await CompleteAsync(factory, binding, after.Id, invoke: false);
        using var scope = factory.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var quota = await db.Set<AssistantPlatformKeyDailyQuota>().AsNoTracking().SingleAsync(row => row.UserId == binding.UserId, Ct);
        Assert.Equal(0, quota.ReservedQueries); Assert.Equal(1, quota.ConsumedQueries);
        Assert.Single(await db.Set<AssistantUsageFact>().Where(row => row.OperationId == after.Id).ToArrayAsync(Ct));
        Assert.Equal(.00015m, (await db.Set<AssistantFinancialCall>().AsNoTracking().SingleAsync(row => row.OperationId == after.Id, Ct)).Cost.Amount);
    }

    [Fact]
    public async Task Mirror_and_private_fact_rollback_together_and_RLS_hides_other_organization_and_platform_has_no_chat_columns()
    {
        var binding = await BusinessAsync();
        var other = await BusinessAsync();
        var operation = (await ReserveAsync(factory, binding, Guid.CreateVersion7())).Value;
        var rollback = await CompleteAsync(factory, binding, operation.Id, invoke: true, rollback: true);
        Assert.True(rollback.IsFailure);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(AssistantFinancialState.Reserved, (await db.Set<AssistantFinancialOperation>().AsNoTracking().SingleAsync(row => row.Id == operation.Id, Ct)).State);
            Assert.Empty(await db.Set<AssistantFinancialCall>().Where(row => row.OperationId == operation.Id).ToArrayAsync(Ct));
        }
        await CompleteAsync(factory, binding, operation.Id, invoke: true);
        using (var scope = factory.Services.CreateScope())
        {
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(other.ScopeTenantId!.Value);
            var services = scope.ServiceProvider;
            Assert.Empty(await services.GetRequiredService<ApplicationDbContext>().Set<AssistantUsageFact>().Where(row => row.OperationId == operation.Id).ToArrayAsync(Ct));
            Assert.Empty(await services.GetRequiredService<IAssistantFinanceReader>().ListBusinessAsync(NowUtc.Date, NowUtc.Date.AddDays(1), Ct));
        }
        using (var scope = factory.Services.CreateScope())
        {
            var row = Assert.Single((await scope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>()
                .ListPlatformAsync(NowUtc.Date, NowUtc.Date.AddDays(1), Ct)), row => row.OrganizationId == binding.ScopeTenantId);
            Assert.Equal(.00015m, row.Cost.Amount); Assert.Equal(1, row.Queries);
            Assert.Equal(1000, row.InputTokens); Assert.Equal(100, row.OutputTokens);
            Assert.Equal(1000m, row.MonthlyBudget!.Amount);
        }
        await using var runtime = new NpgsqlConnection(factory.RuntimeConnectionString);
        await runtime.OpenAsync(Ct);
        await using var privacy = new NpgsqlCommand("""
            SELECT count(*) FROM information_schema.columns WHERE table_schema='platform'
            AND table_name IN ('AssistantFinancialOperations','AssistantFinancialCalls','AssistantBudgetMonths','AssistantPlatformKeyDailyQuotas')
            AND (column_name ILIKE '%Conversation%' OR column_name ILIKE '%Text%' OR column_name ILIKE '%PromptJson%');
            """, runtime);
        Assert.Equal(0L, await privacy.ExecuteScalarAsync(Ct));
        await using var rls = new NpgsqlCommand("SELECT count(*) FROM tenant.\"AssistantUsageFacts\" WHERE \"OperationId\"=@id", runtime);
        rls.Parameters.AddWithValue("id", operation.Id);
        Assert.Equal(0L, await rls.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task Civil_month_uses_real_DST_boundaries_and_zone_change_keeps_the_existing_snapshot()
    {
        var binding = await BusinessAsync();
        var marchUtc = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        var first = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), marchUtc, zone: "America/New_York")).Value;
        var changed = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), marchUtc, zone: "America/Argentina/Buenos_Aires")).Value;
        Assert.Equal(first.BudgetMonthId, changed.BudgetMonthId);
        using var scope = factory.Services.CreateScope();
        var month = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetMonth>().AsNoTracking()
            .SingleAsync(row => row.Id == first.BudgetMonthId, Ct);
        Assert.Equal(new DateTime(2026, 3, 1, 5, 0, 0, DateTimeKind.Utc), month.StartUtc);
        Assert.Equal(new DateTime(2026, 4, 1, 4, 0, 0, DateTimeKind.Utc), month.EndUtc);
        Assert.Equal("America/New_York", month.TimeZoneId);
    }

    [Fact]
    public async Task Technical_reconciliation_survives_content_deletion_without_recreating_private_content()
    {
        var binding = await BusinessAsync();
        var operation = (await ReserveAsync(factory, binding, Guid.CreateVersion7())).Value;
        await WriteAsync(factory, binding, async services =>
        {
            Assert.True((await Writer(services).BeginCallAsync(binding, operation.Id, 1, false, Ct)).IsSuccess);
            return Result.Success();
        });
        await WriteAsync(factory, binding, async services =>
        {
            var participant = services.GetRequiredService<IEnumerable<IAccountDeletionParticipant>>()
                .Single(row => row.GetType().Name == "AssistantAccountDeletionParticipant");
            await participant.ExecuteAsync(new AccountDeletionContext(binding.UserId, NowUtc, binding.ScopeTenantId), Ct);
            return Result.Success();
        });
        await CompleteAsync(factory, binding, operation.Id, invoke: false);
        await CompleteAsync(factory, binding, operation.Id, invoke: false);
        using var scope = factory.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Set<AssistantUsageFact>().Where(row => row.OperationId == operation.Id).ToArrayAsync(Ct));
        var technical = await db.Set<AssistantFinancialCall>().AsNoTracking().SingleAsync(row => row.OperationId == operation.Id, Ct);
        Assert.True(technical.IsEstimated); Assert.Equal(.011264m, technical.Cost.Amount);
        Assert.Equal(1, (await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == binding.UserId, Ct)).ConsumedQueries);
    }

    private async Task<AssistantFinancialBinding> BusinessAsync()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var id = await OrganizationJourney.StageAsync(factory, account, "Finance organization", Ct);
        return new(account.UserId, Access.Business, id);
    }

    [Fact]
    public async Task Zone_change_at_old_month_end_never_assigns_an_operation_to_an_expired_interval()
    {
        var binding = await BusinessAsync();
        var first = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc),
            zone: "America/Argentina/Buenos_Aires")).Value;
        var gapUtc = new DateTime(2026, 11, 1, 3, 30, 0, DateTimeKind.Utc);
        var admission = await ReserveAsync(factory, binding, Guid.CreateVersion7(), gapUtc, zone: "America/New_York");
        Assert.True(admission.IsSuccess);
        using var scope = factory.Services.CreateScope();
        var month = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetMonth>()
            .AsNoTracking().SingleAsync(row => row.Id == admission.Value.BudgetMonthId, Ct);
        Assert.True(month.StartUtc <= gapUtc && gapUtc < month.EndUtc,
            $"Operation {gapUtc:O} is outside frozen {month.TimeZoneId} interval [{month.StartUtc:O}, {month.EndUtc:O}).");
        Assert.Equal(new DateOnly(2026, 11, 1), month.Month);
        Assert.Equal(new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc), month.StartUtc);
        Assert.Equal(new DateTime(2026, 12, 1, 5, 0, 0, DateTimeKind.Utc), month.EndUtc);
        var previous = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetMonth>()
            .AsNoTracking().SingleAsync(row => row.Id == first.BudgetMonthId, Ct);
        Assert.Equal("America/Argentina/Buenos_Aires", previous.TimeZoneId); Assert.Equal(month.StartUtc, previous.EndUtc);
        await CompleteAsync(factory, binding, admission.Value.Id, invoke: true);
        var global = Assert.Single(await scope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>()
            .ListPlatformAsync(month.StartUtc, month.EndUtc, Ct), row => row.OrganizationId == binding.ScopeTenantId);
        Assert.Equal(.00015m, global.Cost.Amount); Assert.Equal(month.StartUtc, global.MonthStartUtc);
        using var privateScope = factory.Services.CreateScope();
        using var tenant = privateScope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
        var privateRow = Assert.Single(await privateScope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>()
            .ListBusinessAsync(month.StartUtc, month.EndUtc, Ct));
        Assert.Equal(global.Cost, privateRow.Cost); Assert.Equal(global.MonthStartUtc, privateRow.MonthStartUtc);
        Assert.Equal(global.MonthEndUtc, privateRow.MonthEndUtc);
    }

    [Fact]
    public async Task Reverse_transition_exact_border_and_successive_zone_changes_preserve_unique_consecutive_periods()
    {
        var binding = await BusinessAsync();
        var october = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc),
            zone: "America/New_York")).Value;
        var before = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), new DateTime(2026, 11, 1, 3, 30, 0, DateTimeKind.Utc),
            zone: "America/Argentina/Buenos_Aires")).Value;
        Assert.Equal(october.BudgetMonthId, before.BudgetMonthId);
        var exact = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc),
            zone: "America/Argentina/Buenos_Aires")).Value;
        var changed = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc), zone: "UTC")).Value;
        Assert.Equal(exact.BudgetMonthId, changed.BudgetMonthId);
        var december = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), new DateTime(2026, 12, 1, 3, 0, 0, DateTimeKind.Utc), zone: "UTC")).Value;
        using var scope = factory.Services.CreateScope();
        var months = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetMonth>().AsNoTracking()
            .Where(row => row.OrganizationId == binding.ScopeTenantId).OrderBy(row => row.Month).ToArrayAsync(Ct);
        Assert.Equal(3, months.Length);
        Assert.Equal(months[0].EndUtc, months[1].StartUtc); Assert.Equal(months[1].EndUtc, months[2].StartUtc);
        Assert.Equal(new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc), months[1].StartUtc);
        Assert.Equal("America/Argentina/Buenos_Aires", months[1].TimeZoneId);
        Assert.Equal(new DateTime(2026, 12, 1, 3, 0, 0, DateTimeKind.Utc), months[2].StartUtc);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), months[2].EndUtc);
        Assert.Equal(months[2].Id, december.BudgetMonthId);
    }

    [Fact]
    public async Task Skipped_empty_months_are_consecutive_and_late_completion_stays_in_the_original_DST_period()
    {
        var binding = await BusinessAsync();
        var march = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc),
            zone: "America/New_York")).Value;
        var july = (await ReserveAsync(factory, binding, Guid.CreateVersion7(), new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc), zone: "UTC")).Value;
        await WriteAsync(factory, binding, async services =>
        {
            var writer = new AssistantUsageWriter(services.GetRequiredService<IAssistantFinanceRepository>(), Clock(new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc)));
            Assert.True((await writer.BeginCallAsync(binding, march.Id, 1, false, Ct)).IsSuccess);
            Assert.True((await writer.RecordUsageAsync(binding, march.Id, 1, new(1000, 100, 0, 0, 0, 0, null, true), Ct)).IsSuccess);
            return await writer.CompleteAsync(binding, march.Id, Ct);
        });
        using var scope = factory.Services.CreateScope();
        var months = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetMonth>().AsNoTracking()
            .Where(row => row.OrganizationId == binding.ScopeTenantId).OrderBy(row => row.Month).ToArrayAsync(Ct);
        Assert.Equal(5, months.Length);
        for (var index = 1; index < months.Length; index++)
        {
            Assert.Equal(months[index - 1].Month.AddMonths(1), months[index].Month);
            Assert.Equal(months[index - 1].EndUtc, months[index].StartUtc);
        }
        Assert.Equal(new DateTime(2026, 4, 1, 4, 0, 0, DateTimeKind.Utc), months[0].EndUtc);
        Assert.Equal(months[^1].Id, july.BudgetMonthId);
        var reader = scope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>();
        var original = Assert.Single(await reader.ListPlatformAsync(months[0].StartUtc, months[0].EndUtc, Ct), row => row.OrganizationId == binding.ScopeTenantId);
        Assert.Equal(.00015m, original.Cost.Amount); Assert.Equal(months[0].Month, original.Month);
        Assert.DoesNotContain(await reader.ListPlatformAsync(months[^1].StartUtc, months[^1].EndUtc, Ct), row => row.OrganizationId == binding.ScopeTenantId);
    }

    [Fact]
    public async Task Five_calls_including_title_replace_cumulative_usage_and_persist_twelve_decimal_places()
    {
        var binding = await BusinessAsync();
        var price = AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "fixture-low", "revision-frozen",
            Usd(.000001m), Usd(.000001m), Usd(.000001m), Usd(.000001m), Usd(.000001m), null, null, null, null, null, null);
        var operation = (await ReserveCoreAsync(factory, binding,
            new(Guid.CreateVersion7(), binding, AssistantMode.PlatformKey, 50, Usd(1000), "UTC", price, price), NowUtc)).Value;
        await WriteAsync(factory, binding, async services =>
        {
            var writer = Writer(services);
            for (var sequence = 1; sequence <= 5; sequence++)
            {
                var call = await writer.BeginCallAsync(binding, operation.Id, sequence, sequence == 5, Ct);
                Assert.True(call.IsSuccess); Assert.True(call.Value.IsNew);
                Assert.True((await writer.RecordUsageAsync(binding, operation.Id, sequence, new(1, 0, 0, 0, 0, 0, null, true), Ct)).IsSuccess);
                Assert.True((await writer.RecordUsageAsync(binding, operation.Id, sequence, new(2, 0, 0, 0, 0, 0, null, true), Ct)).IsSuccess);
            }
            Assert.False((await writer.BeginCallAsync(binding, operation.Id, 6, false, Ct)).IsSuccess);
            var replay = await writer.BeginCallAsync(binding, operation.Id, 5, true, Ct);
            Assert.True(replay.IsSuccess); Assert.False(replay.Value.IsNew);
            return await writer.CompleteAsync(binding, operation.Id, Ct);
        });
        using var scope = factory.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var calls = await db.Set<AssistantFinancialCall>().AsNoTracking().Where(row => row.OperationId == operation.Id).ToArrayAsync(Ct);
        Assert.Equal(5, calls.Length);
        Assert.All(calls, call => { Assert.Equal(2, call.InputTokens); Assert.Equal(.000000000002m, call.Cost.Amount); Assert.Equal("revision-frozen", call.Price.Revision); });
        Assert.Single(calls, call => call.IsTitle);
        var facts = await db.Set<AssistantUsageFact>().Where(row => row.OperationId == operation.Id).ToArrayAsync(Ct);
        Assert.Equal(5, facts.Length); Assert.All(facts, fact => Assert.Equal(.000000000002m, fact.Cost.Amount));
        Assert.Equal(1, (await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == binding.UserId, Ct)).ConsumedQueries);
    }

    [Fact]
    public async Task Persisted_month_of_eight_people_exceeds_int32_without_exceeding_the_allowed_budget()
    {
        var binding = await BusinessAsync();
        var source = (await ReserveAsync(factory, binding, Guid.CreateVersion7())).Value;
        await CompleteAsync(factory, binding, source.Id, invoke: true);
        var startUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddMonths(1);
        var month = AssistantBudgetMonth.Create(binding.ScopeTenantId!.Value, new DateOnly(2026, 4, 1), "UTC", startUtc, endUtc);
        await WriteAsync(factory, binding, async services =>
        {
            services.GetRequiredService<IAssistantFinanceRepository>().Add(month);
            return await Task.FromResult(Result.Success());
        });
        // Technical fixture bulk-loads allowed bounded facts, never provider calls or authorization evidence.
        await WriteAsync(factory, binding, async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            // Only this bounded 48,000-fact seed gets extra time; readers keep their normal timeout.
            db.Database.SetCommandTimeout(180);
            await db.Database.ExecuteSqlAsync($"""
                CREATE TEMP TABLE finance_capacity_users ON COMMIT DROP AS
                    SELECT gen_random_uuid() AS id FROM generate_series(1,8);
                CREATE TEMP TABLE finance_capacity_operations ON COMMIT DROP AS
                    SELECT gen_random_uuid() AS id, u.id AS user_id, {startUtc} + d * interval '1 day' AS occurred
                    FROM finance_capacity_users u CROSS JOIN generate_series(0,29) d CROSS JOIN generate_series(1,50) q;
                INSERT INTO platform."AssistantFinancialOperations"
                    SELECT (jsonb_populate_record(NULL::platform."AssistantFinancialOperations", to_jsonb(s)
                        || jsonb_build_object('Id', c.id, 'UserId', c.user_id, 'DayUtc', c.occurred::date,
                            'CreatedAtUtc', c.occurred, 'CompletedAtUtc', c.occurred, 'BudgetMonthId', {month.Id}))).*
                    FROM finance_capacity_operations c CROSS JOIN platform."AssistantFinancialOperations" s WHERE s."Id"={source.Id};
                CREATE TEMP TABLE finance_capacity_calls ON COMMIT DROP AS
                    SELECT gen_random_uuid() AS id, o.id AS operation_id, o.user_id, o.occurred, seq
                    FROM finance_capacity_operations o CROSS JOIN generate_series(1,4) seq;
                INSERT INTO platform."AssistantFinancialCalls"
                    SELECT (jsonb_populate_record(NULL::platform."AssistantFinancialCalls", to_jsonb(s)
                        || jsonb_build_object('Id', c.id, 'OperationId', c.operation_id, 'Sequence', c.seq,
                            'InputTokens', 51200, 'OutputTokens', 2048, 'ReasoningTokens', NULL, 'CostAmount', 0.006144))).*
                    FROM finance_capacity_calls c CROSS JOIN platform."AssistantFinancialCalls" s WHERE s."OperationId"={source.Id};
                INSERT INTO tenant."AssistantUsageFacts"
                    SELECT (jsonb_populate_record(NULL::tenant."AssistantUsageFacts", to_jsonb(s)
                        || jsonb_build_object('Id', gen_random_uuid(), 'CallId', c.id, 'OperationId', c.operation_id,
                            'UserId', c.user_id, 'DayUtc', c.occurred::date, 'BudgetMonthId', {month.Id},
                            'InputTokens', 51200, 'OutputTokens', 2048, 'CostAmount', 0.006144))).*
                    FROM finance_capacity_calls c CROSS JOIN tenant."AssistantUsageFacts" s WHERE s."OperationId"={source.Id};
                """, Ct);
            return Result.Success();
        });
        // Bulk fixtures bypass the ordinary cadence of autovacuum/analyze; plan against the actual bounded data.
        await using (var seedConnection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await seedConnection.OpenAsync(Ct);
            await using var statistics = new NpgsqlCommand("""
                ANALYZE platform."AssistantFinancialOperations";
                ANALYZE platform."AssistantFinancialCalls";
                ANALYZE tenant."AssistantUsageFacts";
                """, seedConnection);
            await statistics.ExecuteNonQueryAsync(Ct);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var row = Assert.Single(await scope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>().ListPlatformAsync(startUtc, endUtc, Ct));
            Assert.Equal(12_000, row.Queries); Assert.Equal(8, row.PeopleCount);
            Assert.Equal(2_457_600_000L, row.InputTokens); Assert.Equal(98_304_000, row.OutputTokens);
            Assert.Equal(294.912m, row.Cost.Amount); Assert.Equal(1000m, row.MonthlyBudget!.Amount);
            Assert.True(row.InputTokens > int.MaxValue);
            Assert.Throws<OverflowException>(() => checked((int)row.InputTokens));
        }
        using (var scope = factory.Services.CreateScope())
        {
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
            var row = Assert.Single(await scope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>().ListBusinessAsync(startUtc, endUtc, Ct));
            Assert.Equal(2_457_600_000L, row.InputTokens); Assert.Equal(294.912m, row.Cost.Amount);
        }
    }

    [Fact]
    public async Task New_price_revision_does_not_reprice_an_already_admitted_operation()
    {
        var binding = await BusinessAsync();
        var original = Price();
        var old = (await ReserveCoreAsync(factory, binding,
            new(Guid.CreateVersion7(), binding, AssistantMode.PlatformKey, 50, Usd(1000), "UTC", original, null), NowUtc)).Value;
        var changed = AssistantPriceSnapshot.Create(original.ModelId, original.ProviderModelId, "price-v2", Usd(.2m), Usd(1m),
            Usd(.25m), Usd(.4m), Usd(.02m), null, null, null, null, null, null);
        var next = (await ReserveCoreAsync(factory, binding,
            new(Guid.CreateVersion7(), binding, AssistantMode.PlatformKey, 50, Usd(1000), "UTC", changed, null), NowUtc.AddMinutes(1))).Value;
        await CompleteAsync(factory, binding, old.Id, invoke: true);
        await CompleteAsync(factory, binding, next.Id, invoke: true);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var oldCall = await db.Set<AssistantFinancialCall>().AsNoTracking().SingleAsync(row => row.OperationId == old.Id, Ct);
        var newCall = await db.Set<AssistantFinancialCall>().AsNoTracking().SingleAsync(row => row.OperationId == next.Id, Ct);
        Assert.Equal("price-v1", oldCall.Price.Revision); Assert.Equal(.00015m, oldCall.Cost.Amount);
        Assert.Equal("price-v2", newCall.Price.Revision); Assert.Equal(.0003m, newCall.Cost.Amount);
        var row = Assert.Single(await scope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>()
            .ListPlatformAsync(NowUtc.Date, NowUtc.Date.AddDays(1), Ct), row => row.OrganizationId == binding.ScopeTenantId);
        Assert.Equal(.00045m, row.Cost.Amount); Assert.Equal(2, row.Queries);
    }
    [Theory]
    [InlineData(AssistantMode.PlatformKey, AssistantKeyFunding.PlatformKey)]
    [InlineData(AssistantMode.OwnKey, AssistantKeyFunding.OwnKey)]
    public async Task Budget_snapshot_uses_the_same_requested_range_in_both_readers(AssistantMode mode, AssistantKeyFunding funding)
    {
        var binding = await BusinessAsync();
        var other = await BusinessAsync();
        await CompleteAtAsync(binding, NowUtc, 1000);
        await CompleteAtAsync(binding, NowUtc.AddDays(1), 2000);
        await CompleteAtAsync(other, NowUtc, 9000);
        await AssertRangeAsync(NowUtc.Date, NowUtc.Date.AddDays(1), 1, .00015m, 1000);
        await AssertRangeAsync(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 2, .0003m, 2000);

        async Task CompleteAtAsync(AssistantFinancialBinding original, DateTime admittedAtUtc, decimal budget)
        {
            var admitted = await ReserveAsync(factory, original, Guid.CreateVersion7(), admittedAtUtc, mode, budget);
            Assert.True(admitted.IsSuccess, admitted.Error.Code);
            Assert.True((await WriteAsync(factory, original, async services =>
            {
                var writer = new AssistantUsageWriter(services.GetRequiredService<IAssistantFinanceRepository>(), Clock(admittedAtUtc.AddMinutes(1)));
                Assert.True((await writer.BeginCallAsync(original, admitted.Value.Id, 1, false, Ct)).IsSuccess);
                Assert.True((await writer.RecordUsageAsync(original, admitted.Value.Id, 1, new(1000, 100, 0, 0, 0, 0, 30, true), Ct)).IsSuccess);
                return await writer.CompleteAsync(original, admitted.Value.Id, Ct);
            })).IsSuccess);
        }
        async Task AssertRangeAsync(DateTime startUtc, DateTime endUtc, long queries, decimal cost, decimal budget)
        {
            using var scope = factory.Services.CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>();
            var platform = Assert.Single(await reader.ListPlatformAsync(startUtc, endUtc, Ct),
                row => row.OrganizationId == binding.ScopeTenantId && row.Funding == funding);
            Assert.Equal(queries, platform.Queries); Assert.Equal(cost, platform.Cost.Amount);
            Assert.Equal(budget, platform.MonthlyBudget!.Amount);
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
            var business = Assert.Single(await reader.ListBusinessAsync(startUtc, endUtc, Ct));
            Assert.Equal(platform.OrganizationId, business.OrganizationId); Assert.Equal(funding, business.Funding);
            Assert.Equal(platform.Queries, business.Queries); Assert.Equal(platform.Cost, business.Cost);
            Assert.Equal(platform.InputTokens, business.InputTokens); Assert.Equal(platform.OutputTokens, business.OutputTokens);
            Assert.Equal(platform.Month, business.Month); Assert.Equal(platform.MonthStartUtc, business.MonthStartUtc);
            Assert.Equal(platform.MonthEndUtc, business.MonthEndUtc);
            Assert.Equal(platform.MonthlyBudget, business.MonthlyBudget);
        }
    }

    [Fact]
    public void Every_financial_money_column_has_a_restrict_foreign_key_to_the_currency_catalog()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GetService<IDesignTimeModel>().Model;
        foreach (var table in model.GetRelationalModel().Tables.Where(table => table.Name is
            "AssistantUsageFacts" or "AssistantFinancialCalls" or "AssistantFinancialOperations"))
        {
            var currencies = table.Columns.Where(column => column.Name.EndsWith("Currency", StringComparison.Ordinal)).ToArray();
            Assert.NotEmpty(currencies);
            foreach (var column in currencies)
                Assert.Contains(table.ForeignKeyConstraints, foreignKey => foreignKey.Columns.Contains(column)
                    && foreignKey.PrincipalTable.Schema == "platform" && foreignKey.PrincipalTable.Name == "Currencies"
                    && foreignKey.OnDeleteAction == Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict);
        }
    }

    [Theory]
    [InlineData("tenant", "AssistantUsageFacts", 1)]
    [InlineData("platform", "AssistantFinancialCalls", 12)]
    [InlineData("platform", "AssistantFinancialOperations", 24)]
    public async Task Every_financial_currency_rejects_an_unknown_alpha3_even_when_the_money_is_optional(string schema, string table, int expectedColumns)
    {
        var (binding, operation) = await CompleteCurrencyFixtureAsync();
        await using var connection = new NpgsqlConnection(factory.RuntimeConnectionString);
        await connection.OpenAsync(Ct);
        await using var catalog = new NpgsqlCommand("SELECT count(*) FROM platform.\"Currencies\" WHERE \"Code\"='ZZZ'", connection);
        Assert.Equal(0L, await catalog.ExecuteScalarAsync(Ct));
        Assert.True(CurrencyCode.Create("ZZZ").IsSuccess);
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GetService<IDesignTimeModel>().Model;
        var columns = model.GetRelationalModel().FindTable(table, schema)!.Columns
            .Where(column => column.Name.EndsWith("Currency", StringComparison.Ordinal)).OrderBy(column => column.Name).ToArray();
        Assert.Equal(expectedColumns, columns.Length);
        foreach (var column in columns)
        {
            await using var transaction = await connection.BeginTransactionAsync(Ct);
            await using var tenant = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction);
            tenant.Parameters.AddWithValue("tenant", binding.ScopeTenantId!.Value.ToString());
            await tenant.ExecuteNonQueryAsync(Ct);
            // Identifiers come exclusively from this model's three explicit tables, never request input.
            var filter = table == "AssistantFinancialOperations" ? "Id" : "OperationId";
            await using var update = new NpgsqlCommand($"UPDATE {schema}.\"{table}\" SET \"{column.Name}\"=@code WHERE \"{filter}\"=@id AND \"{column.Name}\" IS NOT NULL", connection, transaction);
            update.Parameters.AddWithValue("code", "ZZZ"); update.Parameters.AddWithValue("id", operation.Id);
            var error = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync(Ct));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
            await transaction.RollbackAsync(Ct);
        }
    }

    [Fact]
    public async Task Disabling_a_currency_preserves_financial_history_and_both_readers()
    {
        var (binding, operation) = await CompleteCurrencyFixtureAsync();
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await using var disable = new NpgsqlCommand("UPDATE platform.\"Currencies\" SET \"IsEnabled\"=false WHERE \"Code\"='USD'", connection, transaction);
        Assert.Equal(1, await disable.ExecuteNonQueryAsync(Ct));
        await transaction.CommitAsync(Ct);
        try
        {
            using var scope = factory.Services.CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<IAssistantFinanceReader>();
            Assert.Equal("USD", Assert.Single(await reader.ListPlatformAsync(NowUtc.Date, NowUtc.Date.AddDays(1), Ct), row => row.OrganizationId == binding.ScopeTenantId).Cost.Currency.Value);
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var history = await db.Set<AssistantFinancialOperation>().AsNoTracking().SingleAsync(row => row.Id == operation.Id, Ct);
            Assert.Equal("USD", history.MonthlyBudget!.Currency.Value);
            Assert.Equal("USD", history.TitlePrice!.LongCacheRead!.Currency.Value);
            Assert.Equal("USD", Assert.Single(await reader.ListBusinessAsync(NowUtc.Date, NowUtc.Date.AddDays(1), Ct)).Cost.Currency.Value);
        }
        finally
        {
            await using var restore = new NpgsqlCommand("UPDATE platform.\"Currencies\" SET \"IsEnabled\"=true WHERE \"Code\"='USD'", connection);
            Assert.Equal(1, await restore.ExecuteNonQueryAsync(Ct));
        }
    }

    private async Task<(AssistantFinancialBinding Binding, AssistantFinancialOperation Operation)> CompleteCurrencyFixtureAsync()
    {
        var binding = await BusinessAsync();
        var price = AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "fixture-currency", "currency-v1",
            Usd(.1m), Usd(.5m), Usd(.125m), Usd(.2m), Usd(.01m), 20000,
            Usd(.2m), Usd(1m), Usd(.25m), Usd(.4m), Usd(.02m));
        var operation = (await ReserveCoreAsync(factory, binding,
            new(Guid.CreateVersion7(), binding, AssistantMode.PlatformKey, 50, Usd(1000), "UTC", price, price), NowUtc)).Value;
        Assert.True((await CompleteAsync(factory, binding, operation.Id, invoke: true)).IsSuccess);
        return (binding, operation);
    }

    private static Task<Result<AssistantFinancialOperation>> ReserveAsync(WebApplicationFactory<Program> host,
        AssistantFinancialBinding binding, Guid operationId, DateTime? nowUtc = null, AssistantMode mode = AssistantMode.PlatformKey,
        decimal budget = 1000m, string zone = "UTC") => ReserveCoreAsync(host, binding,
            new(operationId, binding, mode, 50, Usd(budget), zone, Price(), null), nowUtc ?? NowUtc);
    private static async Task<Result<AssistantFinancialOperation>> ReserveCoreAsync(WebApplicationFactory<Program> host,
        AssistantFinancialBinding binding, AssistantFinanceAdmission admission, DateTime nowUtc)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = binding.ScopeTenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        var guard = new AssistantLimitGuard(services.GetRequiredService<IAssistantFinanceRepository>(), services.GetRequiredService<ITimeZoneService>(), Clock(nowUtc));
        return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => guard.ReserveAsync(admission, Ct), CommitPolicy.OnSuccess, Ct);
    }
    private static AssistantUsageWriter Writer(IServiceProvider services) => new(services.GetRequiredService<IAssistantFinanceRepository>(), Clock(NowUtc));
    private static async Task<Result> WriteAsync(WebApplicationFactory<Program> host, AssistantFinancialBinding binding, Func<IServiceProvider, Task<Result>> work)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = binding.ScopeTenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => work(services), CommitPolicy.OnSuccess, Ct);
    }
    private static Task<Result> CompleteAsync(WebApplicationFactory<Program> host, AssistantFinancialBinding binding, Guid operationId, bool invoke, bool rollback = false) =>
        WriteAsync(host, binding, async services =>
        {
            var writer = Writer(services);
            if (invoke)
            {
                var admission = await writer.BeginCallAsync(binding, operationId, 1, false, Ct);
                Assert.True(admission.IsSuccess, admission.Error.Code);
                await writer.RecordUsageAsync(binding, operationId, 1, new(1000, 100, 0, 0, 0, 0, 30, true), Ct);
            }
            var result = await writer.CompleteAsync(binding, operationId, Ct);
            return rollback ? AssistantErrors.ProviderUnavailable : result;
        });
    private static async Task AdmitAndCompleteAsync(WebApplicationFactory<Program> host, AssistantFinancialBinding binding)
    {
        var admission = await ReserveAsync(host, binding, Guid.CreateVersion7());
        Assert.True(admission.IsSuccess, admission.Error.Code);
        Assert.True((await CompleteAsync(host, binding, admission.Value.Id, invoke: true)).IsSuccess);
    }
}
