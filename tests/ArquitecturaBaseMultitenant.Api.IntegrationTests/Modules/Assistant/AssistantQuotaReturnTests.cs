using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

// ADR 0064 §1 and §12: a turn that never reserved a step with the provider gives back the person's daily quota and the
// organization's monthly reservation, whether it is recovered or expires; one that did, keeps them spent. A copy takes four
// pending turns per pass.
[Collection(ApiTestGroup.Name)]
public sealed class AssistantQuotaReturnTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Recovering_a_turn_that_never_reached_the_provider_gives_back_the_daily_quota_and_the_monthly_reservation()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false);
        var (turnId, stream) = await OpenTurnAsync(session);
        using (stream)
        {
            await AssertSpendingAsync(session, turnId, reserved: 1, consumed: 0, monthly: amount => Assert.True(amount > 0m));

            // The worker recovers it with the organization's scope only, as when the person can no longer run it.
            using (var scope = session.Host.Services.CreateScope())
            {
                using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
                var recovered = await scope.ServiceProvider.GetRequiredService<IAssistantTurnService>().RecoverAsync(turnId, Guid.NewGuid(), Ct);
                Assert.True(recovered.IsSuccess, recovered.Error.Code);
            }

            await AssertSpendingAsync(session, turnId, reserved: 0, consumed: 0, monthly: amount => Assert.Equal(0m, amount),
                AssistantFinancialState.Released);
            Assert.Equal(0, session.Provider.Calls);
        }
    }

    [Fact]
    public async Task A_turn_that_expires_before_reaching_the_provider_gives_back_the_daily_quota_and_the_monthly_reservation()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false);
        var (turnId, stream) = await OpenTurnAsync(session);
        using (stream)
        {
            await UpdateWorkAsync(session, turnId, (db, work, nowUtc) =>
                db.Entry(work).Property(row => row.DeadlineAtUtc).CurrentValue = nowUtc.AddSeconds(-1));
            await using (var replica = session.Replica())
            {
                using var replicaClient = AssistantTurnTestSession.NewClient(replica);
                var terminal = await stream.TerminalAsync(Ct);
                Assert.Equal("error", terminal.Event);
            }
            await AssertSpendingAsync(session, turnId, reserved: 0, consumed: 0, monthly: amount => Assert.Equal(0m, amount),
                AssistantFinancialState.Released);
            Assert.Equal(0, session.Provider.Calls);
        }
    }

    [Fact]
    public async Task A_turn_sealed_by_a_newer_engine_waits_for_its_copy_and_once_expired_gives_back_the_quota_without_the_provider()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false);
        var (turnId, stream) = await OpenTurnAsync(session);
        using (stream)
        {
            // 0064 §6: lo selló una versión más nueva, que esta copia no sabe armar.
            await UpdateWorkAsync(session, turnId, (db, work, _) =>
                db.Entry(work).Property(row => row.EngineVersion).CurrentValue = AssistantTurnWork.CurrentEngineVersion + 1);
            Assert.Null(await ReadyAsync(session, turnId));
            await AssertSpendingAsync(session, turnId, reserved: 1, consumed: 0, monthly: amount => Assert.True(amount > 0m));
            Assert.Equal(AssistantTurnStatus.Pending, await StatusAsync(session, turnId));

            await UpdateWorkAsync(session, turnId, (db, work, nowUtc) =>
                db.Entry(work).Property(row => row.DeadlineAtUtc).CurrentValue = nowUtc.AddSeconds(-1));
            Assert.False((await ReadyAsync(session, turnId))!.KnownEngine);
            await using (var replica = session.Replica())
            {
                using var replicaClient = AssistantTurnTestSession.NewClient(replica);
                Assert.Equal("error", (await stream.TerminalAsync(Ct)).Event);
            }
            await AssertSpendingAsync(session, turnId, reserved: 0, consumed: 0, monthly: amount => Assert.Equal(0m, amount),
                AssistantFinancialState.Released);
            Assert.Equal(AssistantTurnStatus.Failed, await StatusAsync(session, turnId));
            Assert.Equal(0, session.Provider.Calls);
            Assert.Empty(session.Provider.Invocations);
        }
    }

    [Fact]
    public async Task A_turn_that_reserved_a_step_with_the_provider_keeps_the_daily_quota_and_the_monthly_cost_spent()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false);
        var (turnId, stream) = await OpenTurnAsync(session);
        using (stream)
        {
            var captured = await AssistantExecutionActorTests.CaptureAsync(session.Client);
            using (var scope = session.Host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var actor = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(captured, Ct);
                Assert.True(actor.IsSuccess, actor.Error.Code);
                using var entered = services.GetRequiredService<IExecutionActorScope>().Enter(actor.Value);
                var turns = services.GetRequiredService<IAssistantTurnService>();
                var lease = Guid.NewGuid();
                var claim = await turns.ClaimAsync(turnId, lease, Ct);
                Assert.True(claim.IsSuccess, claim.Error.Code);
                var invoked = await turns.BeginCallAsync(turnId, lease, 1, Ct);
                Assert.True(invoked.IsSuccess, invoked.Error.Code);
            }
            // The copy that held it is gone: another one recovers it without calling the provider again.
            await UpdateWorkAsync(session, turnId, (db, work, nowUtc) =>
                db.Entry(work).Property(row => row.LeaseExpiresAtUtc).CurrentValue = nowUtc.AddSeconds(-1));
            await using (var replica = session.Replica())
            {
                using var replicaClient = AssistantTurnTestSession.NewClient(replica);
                Assert.Equal("error", (await stream.TerminalAsync(Ct)).Event);
            }
            await AssertSpendingAsync(session, turnId, reserved: 0, consumed: 1, monthly: amount => Assert.True(amount > 0m),
                AssistantFinancialState.Completed);
            Assert.Equal(0, session.Provider.Calls);
        }
    }

    [Fact]
    public async Task A_copy_takes_the_four_oldest_ready_turns_per_pass()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                for (var index = 0; index < ids.Length; index++)
                    db.Add(AssistantTurnWork.Create(ids[index], Guid.NewGuid(), Access.Platform, null, null, "es-AR",
                        "America/Argentina/Buenos_Aires", null, Guid.NewGuid(), "fixture-model", "Low", AssistantKeyFunding.PlatformKey,
                        nowUtc.AddMinutes(index - ids.Length), nowUtc.AddHours(1)));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(saved.IsSuccess, saved.Error.Code);
        }
        using var reading = factory.Services.CreateScope();
        var ready = await reading.ServiceProvider.GetRequiredService<IAssistantTurnRepository>().ListReadyAsync(nowUtc, Ct);
        Assert.Equal(AssistantTechnicalLimits.TurnsPerCopy, ready.Count);
        Assert.Equal(ids.Take(4), ready.Select(row => row.TurnId));
    }

    private static async Task<(Guid TurnId, AssistantSseTestReader Stream)> OpenTurnAsync(AssistantTurnTestSession session)
    {
        var conversation = await session.ConversationAsync();
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        var response = await session.OpenStreamAsync(send, Ct);
        var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var start = await stream.NextAsync(Ct);
        Assert.Equal("start", start.Event);
        return (start.Data.GetProperty("turnId").GetGuid(), stream);
    }

    private static async Task UpdateWorkAsync(AssistantTurnTestSession session, Guid turnId, Action<ApplicationDbContext, AssistantTurnWork, DateTime> change)
    {
        using var scope = session.Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var updated = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var work = await db.Set<AssistantTurnWork>().SingleAsync(row => row.Id == turnId, ct);
            change(db, work, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(updated.IsSuccess, updated.Error.Code);
    }

    private static async Task<AssistantTurnDiscovery?> ReadyAsync(AssistantTurnTestSession session, Guid turnId)
    {
        using var scope = session.Host.Services.CreateScope();
        var nowUtc = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var ready = await scope.ServiceProvider.GetRequiredService<IAssistantTurnRepository>().ListReadyAsync(nowUtc, Ct);
        return ready.SingleOrDefault(row => row.TurnId == turnId);
    }

    private static async Task<AssistantTurnStatus> StatusAsync(AssistantTurnTestSession session, Guid turnId)
    {
        using var scope = session.Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantTurnWork>().AsNoTracking()
            .Where(row => row.Id == turnId).Select(row => row.Status).SingleAsync(Ct);
    }

    // What the admission of the next turn sees: the person's quota of the day and what the organization's month has committed
    // or reserved, read by the same repository the guard uses.
    private static async Task AssertSpendingAsync(AssistantTurnTestSession session, Guid turnId, int reserved, int consumed,
        Action<decimal> monthly, AssistantFinancialState? state = null)
    {
        using var scope = session.Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
        var checkedSpending = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var finance = services.GetRequiredService<IAssistantFinanceRepository>();
            var binding = new AssistantFinancialBinding(session.Account.UserId, Access.Business, session.TenantId);
            var operation = await finance.GetOperationAsync(binding, turnId, ct);
            Assert.NotNull(operation);
            if (state is { } expected) Assert.Equal(expected, operation.State);
            var quota = await finance.GetPlatformQuotaAsync(session.Account.UserId, session.TenantId, operation.DayUtc, ct);
            Assert.NotNull(quota);
            Assert.Equal(reserved, quota.ReservedQueries);
            Assert.Equal(consumed, quota.ConsumedQueries);
            Assert.NotNull(operation.BudgetMonthId);
            monthly(await finance.GetCommittedAndReservedAmountAsync(operation.BudgetMonthId.Value, AssistantKeyFunding.PlatformKey, ct));
            return Result.Success();
        }, CommitPolicy.OnAnyResult, Ct);
        Assert.True(checkedSpending.IsSuccess, checkedSpending.Error.Code);
    }
}
