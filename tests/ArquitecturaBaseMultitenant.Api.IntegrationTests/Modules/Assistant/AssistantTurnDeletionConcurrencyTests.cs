using System.Data.Common;
using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantTurnDeletionConcurrencyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deletion_waits_for_recovery_work_before_taking_finance_and_both_transactions_finish_without_recreating_content()
    {
        var pause = new LockPause();
        await using var session = await AssistantTurnTestSession.CreateAsync(workerEnabled: false, databaseObserver: pause);
        var conversation = await session.ConversationAsync();
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var start = await stream.NextAsync(Ct);
        var turnId = start.Data.GetProperty("turnId").GetGuid();
        var lease = Guid.NewGuid();
        var captured = await AssistantExecutionActorTests.CaptureAsync(session.Client);
        using (var setup = session.Host.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            var actor = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(captured, Ct);
            Assert.True(actor.IsSuccess, actor.Error.Code);
            using var entered = services.GetRequiredService<IExecutionActorScope>().Enter(actor.Value);
            var turns = services.GetRequiredService<IAssistantTurnService>();
            var claimed = await turns.ClaimAsync(turnId, lease, Ct);
            Assert.True(claimed.IsSuccess, claimed.Error.Code);
            var invoked = await turns.BeginCallAsync(turnId, lease, 1, Ct);
            Assert.True(invoked.IsSuccess, invoked.Error.Code);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var recoveryScope = session.Host.Services.CreateScope();
        var recoveryServices = recoveryScope.ServiceProvider;
        using var recoveryTenant = recoveryServices.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
        var recoveryDb = recoveryServices.GetRequiredService<ApplicationDbContext>();
        pause.RecoveryContext = recoveryDb;
        var recovery = recoveryServices.GetRequiredService<IAssistantTurnService>().RecoverAsync(turnId, lease, deadline.Token);
        using var deletionScope = session.Host.Services.CreateScope();
        var deletionServices = deletionScope.ServiceProvider;
        using var deletionTenant = deletionServices.GetRequiredService<ITenantScope>().Enter(session.TenantId.Value);
        var deletionDb = deletionServices.GetRequiredService<ApplicationDbContext>();
        pause.DeletionContext = deletionDb;
        Task<Result>? deletion = null;
        try
        {
            await pause.RecoveryBeforeFinance.Task.WaitAsync(deadline.Token);
            Assert.NotSame(recoveryDb.Database.GetDbConnection(), deletionDb.Database.GetDbConnection());
            deletion = deletionServices.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var participant = deletionServices.GetRequiredService<IEnumerable<IAccountDeletionParticipant>>()
                    .Single(value => value.GetType().Name == "AssistantAccountDeletionParticipant");
                await participant.ExecuteAsync(new AccountDeletionContext(session.Account.UserId,
                    deletionServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, session.TenantId), ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, deadline.Token);
            await pause.DeletionBeforeWork.Task.WaitAsync(deadline.Token);
            // Recovery holds Work while paused. Deletion must not own finance and wait for that same Work.
            Assert.False(pause.DeletionAttemptedFinance.Task.IsCompleted);
            pause.Resume.TrySetResult();
            var recovered = await recovery.WaitAsync(deadline.Token);
            Assert.True(recovered.IsSuccess, recovered.Error.Code);
            var deleted = await deletion.WaitAsync(deadline.Token);
            Assert.True(deleted.IsSuccess, deleted.Error.Code);
        }
        finally
        {
            pause.Resume.TrySetResult();
            if (deletion is not null) await Task.WhenAll(recovery, deletion).WaitAsync(deadline.Token);
            else await recovery.WaitAsync(deadline.Token);
        }
        Assert.Equal(0, session.Provider.Calls);
        using var verification = session.Host.Services.CreateScope();
        var servicesAfter = verification.ServiceProvider;
        using var tenantAfter = servicesAfter.GetRequiredService<ITenantScope>().Enter(session.TenantId.Value);
        var db = servicesAfter.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<AssistantTurnWork>().AnyAsync(row => row.Id == turnId, Ct));
        Assert.False(await db.Set<AssistantConversation>().AnyAsync(row => row.Id == conversation.Id, Ct));
        Assert.False(await db.Set<AssistantMessage>().AnyAsync(row => row.ConversationId == conversation.Id, Ct));
        var operation = await db.Set<AssistantFinancialOperation>().SingleAsync(row => row.Id == turnId, Ct);
        Assert.Equal(AssistantFinancialState.Completed, operation.State);
        Assert.True(operation.ContentDeleted);
        Assert.NotEqual(session.Account.UserId, operation.UserId);
        Assert.Null(operation.ScopeTenantId);
        var quota = await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(1, quota.ConsumedQueries);
        Assert.Equal(0, quota.ReservedQueries);
        using var hidden = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    private sealed class LockPause : DbCommandInterceptor
    {
        internal ApplicationDbContext? RecoveryContext { get; set; }
        internal ApplicationDbContext? DeletionContext { get; set; }
        internal TaskCompletionSource RecoveryBeforeFinance { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource DeletionBeforeWork { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource DeletionAttemptedFinance { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(eventData.Context, DeletionContext)
                && command.CommandText.Contains("\"AssistantTurnWorks\"", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
                DeletionBeforeWork.TrySetResult();
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var finance = command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is string value
                    && value.Contains(":assistant:finance", StringComparison.Ordinal));
            if (finance && ReferenceEquals(eventData.Context, DeletionContext)) DeletionAttemptedFinance.TrySetResult();
            if (finance && ReferenceEquals(eventData.Context, RecoveryContext) && !RecoveryBeforeFinance.Task.IsCompleted)
            {
                RecoveryBeforeFinance.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
