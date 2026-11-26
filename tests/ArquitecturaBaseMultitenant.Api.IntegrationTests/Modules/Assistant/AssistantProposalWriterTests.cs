using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// ADR 0068 §8 al atar los sujetos del turno: una propuesta por mensaje, con el código de esa regla; y la nueva anterior se
/// reemplaza tomándola con lock (guardado.md), así un intento de guardarla que la tiene termina antes, conserva su clave y
/// el turno no pierde su propuesta con un conflicto de concurrencia.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantProposalWriterTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_second_proposal_for_the_message_of_the_turn_breaks_the_one_per_turn_rule()
    {
        var (owner, tenantId, actor) = await SessionAsync("Una propuesta por turno");
        var conversation = await AssistantTestData.ConversationAsync(factory, tenantId, owner, Access.Business);
        var first = await AssistantTestData.ProposalAsync(factory, tenantId, conversation.Id, owner, Action.Id, Action.Version, Values());

        var saved = await SaveAsTurnAsync(actor, conversation.Id, first.MessageId);

        Assert.True(saved.IsFailure);
        Assert.Equal(AssistantErrors.ActionOnePerTurnCode, saved.Error.Code);
    }

    [Fact]
    public async Task Replacing_the_previous_proposal_waits_for_an_attempt_that_holds_it_and_keeps_its_key()
    {
        var (owner, tenantId, actor) = await SessionAsync("Reemplazo con un intento");
        var conversation = await AssistantTestData.ConversationAsync(factory, tenantId, owner, Access.Business);
        var previous = await AssistantTestData.ProposalAsync(factory, tenantId, conversation.Id, owner, Action.Id, Action.Version, Values());
        var nextMessage = await MessageAsync(tenantId, conversation.Id);
        var attemptKey = Guid.NewGuid();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // POST attempts: toma la anterior FOR UPDATE, suma su clave y confirma recién cuando se lo sueltan.
        var attempt = Task.Run(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var held = await services.GetRequiredService<IAssistantProposalRepository>().LockAsync(owner, Access.Business, previous.Id, ct);
                Assert.NotNull(held);
                held.AddAttemptKey(attemptKey, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                locked.SetResult();
                await release.Task.WaitAsync(ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }, Ct);
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);

        var turn = SaveAsTurnAsync(actor, conversation.Id, nextMessage);
        await WaitForProposalLockAsync(turn);
        release.SetResult();

        Assert.True((await attempt).IsSuccess);
        var saved = await turn;
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
        var stored = await StoredAsync(tenantId, previous.Id);
        Assert.Equal(AssistantProposalStatus.Replaced, stored.State);
        Assert.Equal(attemptKey, Assert.Single(stored.ReadAttemptKeys()).Key);
    }

    private static AssistantActionDefinition Action => RoleActionSource.Actions[0];

    private static AssistantProposalValues Values() => new(null, [AssistantTestData.Written("name", "Ventas", "ventas-id")]);

    private async Task<(Guid Owner, Guid TenantId, ExecutionActor Actor)> SessionAsync(string organization)
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, account, organization, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        return (account.UserId, tenantId, await AssistantExecutionActorTests.CaptureAsync(client));
    }

    // Como el turno: con el actor revalidado del worker, ata la propuesta aceptada al mensaje en su transacción.
    private async Task<Result> SaveAsTurnAsync(ExecutionActor actor, Guid conversationId, Guid messageId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var prepared = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        Assert.True(prepared.IsSuccess, prepared.IsFailure ? prepared.Error.Code : null);
        using (services.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value))
        {
            var turn = AssistantTurn.Create(conversationId, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            turn.Bind(Guid.NewGuid(), messageId, Guid.NewGuid());
            var accepted = new AssistantActionToolData
            {
                Outcome = AssistantActionToolData.Accepted,
                ProposalId = Guid.CreateVersion7(),
                Resolution = new AssistantActionResolution(Action, null, null, Values().Values),
            };
            return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
                ct => services.GetRequiredService<AssistantProposalWriter>().SaveAsync(turn, accepted, ct), CommitPolicy.OnSuccess, Ct);
        }
    }

    private async Task<Guid> MessageAsync(Guid tenantId, Guid conversationId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var message = AssistantMessage.Create(conversationId, "Assistant", "Complete", "Te preparé otra propuesta.",
            services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(message);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
        return message.Id;
    }

    private async Task<AssistantProposal> StoredAsync(Guid tenantId, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await services.GetRequiredService<ApplicationDbContext>().Set<AssistantProposal>().AsNoTracking().SingleAsync(row => row.Id == id, Ct);
    }

    // El turno espera la fila que tiene el intento: con el lock, al leerla; sin él, recién al escribirla.
    private async Task WaitForProposalLockAsync(Task turn)
    {
        // PostgreSQL oculta la consulta de mt_app a mt_owner; el bootstrap permite observar sus locks.
        await using var connection = new NpgsqlConnection(factory.BootstrapConnectionString);
        await connection.OpenAsync(Ct);
        for (var attempt = 0; attempt < 500; attempt++)
        {
            if (turn.IsCompleted) return;
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%AssistantProposals%')";
            if ((bool)(await command.ExecuteScalarAsync(Ct))!) return;
            await Task.Delay(10, Ct);
        }
        Assert.Fail("The turn did not wait for the proposal held by the attempt.");
    }
}
