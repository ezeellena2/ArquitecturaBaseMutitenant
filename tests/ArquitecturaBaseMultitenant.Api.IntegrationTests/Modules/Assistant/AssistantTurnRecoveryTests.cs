using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantTurnRecoveryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stopping_before_the_first_invocation_releases_the_reservation_without_consuming_a_query()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync(workerEnabled: false);
        var conversation = await session.ConversationAsync();
        var body = AssistantTurnTestSession.Body(conversation);
        var key = Guid.NewGuid();
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", body, key);
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var start = await stream.NextAsync(Ct);
        Assert.Equal("start", start.Event);
        var turnId = start.Data.GetProperty("turnId").GetGuid();
        using var stop = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/turns/{turnId:D}/stop", "{}", Guid.NewGuid());
        using var stopped = await session.Client.SendAsync(stop, Ct);
        Assert.Equal(HttpStatusCode.NoContent, stopped.StatusCode);
        await using var replica = session.Replica();
        using var replicaClient = AssistantTurnTestSession.NewClient(replica);
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("done", terminal.Event);
        var result = terminal.Data.Deserialize<AssistantTurnResultResponse>(AssistantTurnTestSession.Json)!;
        Assert.Equal(AssistantMessageStatus.Stopped, result.Message.Status);
        Assert.Equal(string.Empty, result.Message.Text);
        Assert.Equal(0m, result.Usage.Cost.Amount);
        using var replay = await AssistantStreamingTests.CompletedReplayAsync(session, conversation.Id, body, key);
        using var json = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct));
        Assert.True(JsonElement.DeepEquals(terminal.Data, json.RootElement));
        Assert.Equal(0, session.Provider.Calls);
        using var scope = session.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var operation = await db.Set<AssistantFinancialOperation>().SingleAsync(row => row.Id == turnId, Ct);
        Assert.Equal(AssistantFinancialState.Released, operation.State);
        Assert.Empty(await db.Set<AssistantFinancialCall>().Where(row => row.OperationId == turnId).ToArrayAsync(Ct));
        var quota = await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(0, quota.ConsumedQueries);
        Assert.Equal(0, quota.ReservedQueries);
    }

    [Fact]
    public async Task Replica_recovers_an_abandoned_invocation_without_repeating_its_possible_external_effect()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync(workerEnabled: false);
        var conversation = await session.ConversationAsync();
        var body = AssistantTurnTestSession.Body(conversation);
        var key = Guid.NewGuid();
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", body, key);
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var start = await stream.NextAsync(Ct);
        Assert.Equal("start", start.Event);
        var turnId = start.Data.GetProperty("turnId").GetGuid();
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
            Assert.False(claim.Value.RecoverOnly);
            var invoked = await turns.BeginCallAsync(turnId, lease, 1, Ct);
            Assert.True(invoked.IsSuccess, invoked.Error.Code);
            // Simula la pérdida del proceso después del marcador durable, sin inventar usage ni una respuesta.
            var expired = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var work = await db.Set<AssistantTurnWork>().SingleAsync(row => row.Id == turnId, ct);
                db.Entry(work).Property(row => row.LeaseExpiresAtUtc).CurrentValue =
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddSeconds(-1);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(expired.IsSuccess, expired.Error.Code);
        }
        await using var replica = session.Replica();
        using var replicaClient = AssistantTurnTestSession.NewClient(replica);
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("error", terminal.Event);
        Assert.Equal(AssistantErrors.ProviderUnavailableCode, terminal.Data.GetProperty("code").GetString());
        var history = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(history);
        Assert.Null(history.ActiveTurnId);
        Assert.Contains(history.Messages.Items, row => row.Status == AssistantMessageStatus.Failed && row.Text == string.Empty);
        using var replay = await AssistantStreamingTests.CompletedReplayAsync(session, conversation.Id, body, key);
        var result = (await replay.Content.ReadFromJsonAsync<AssistantTurnResultResponse>(AssistantTurnTestSession.Json, Ct))!;
        Assert.Equal(AssistantMessageStatus.Failed, result.Message.Status);
        Assert.True(result.Usage.Cost.Amount > 0m);
        Assert.Equal(0, session.Provider.Calls);
        Assert.Empty(session.Provider.Invocations);
        using var finalScope = session.Host.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var operation = await finalDb.Set<AssistantFinancialOperation>().SingleAsync(row => row.Id == turnId, Ct);
        Assert.Equal(AssistantFinancialState.Completed, operation.State);
        var call = await finalDb.Set<AssistantFinancialCall>().SingleAsync(row => row.OperationId == turnId, Ct);
        Assert.True(call.IsEstimated);
        Assert.True(call.IsFinal);
        var quota = await finalDb.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(1, quota.ConsumedQueries);
        Assert.Equal(0, quota.ReservedQueries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_failure_or_missing_usage_finishes_conservatively_and_replay_never_invokes_again(bool missingUsage)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        session.Provider.FailAfterPartial = !missingUsage;
        session.Provider.OmitUsage = missingUsage;
        var conversation = await session.ConversationAsync();
        var prompt = "isolated-private-prompt-not-for-logs";
        var body = AssistantTurnTestSession.Body(conversation, prompt);
        var key = Guid.NewGuid();
        var path = session.Path + $"/conversations/{conversation.Id:D}/messages";
        session.Provider.Logs.Clear();
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, path, body, key);
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var start = await stream.NextAsync(Ct);
        Assert.Equal("start", start.Event);
        var turnId = start.Data.GetProperty("turnId").GetGuid();
        var assistantId = start.Data.GetProperty("assistantMessageId").GetGuid();
        Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);
        session.Provider.Release();
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal(missingUsage ? "done" : "error", terminal.Event);
        if (!missingUsage)
        {
            Assert.Equal(turnId, terminal.Data.GetProperty("turnId").GetGuid());
            Assert.Equal(AssistantErrors.ProviderUnavailableCode, terminal.Data.GetProperty("code").GetString());
            Assert.Equal("Failed", terminal.Data.GetProperty("status").GetString());
            Assert.False(string.IsNullOrWhiteSpace(terminal.Data.GetProperty("traceId").GetString()));
            Assert.False(terminal.Data.TryGetProperty("detail", out _));
        }
        Assert.DoesNotContain(AssistantTurnTestProvider.SensitiveProviderError, terminal.Data.GetRawText(), StringComparison.Ordinal);
        var history = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(history);
        Assert.Null(history.ActiveTurnId);
        var message = history.Messages.Items.Single(row => row.Id == assistantId);
        Assert.Equal(missingUsage ? AssistantMessageStatus.Complete : AssistantMessageStatus.Failed, message.Status);
        Assert.Equal(missingUsage ? AssistantTurnTestProvider.Answer : AssistantTurnTestProvider.Prefix, message.Text);

        using var replay = await AssistantStreamingTests.CompletedReplayAsync(session, conversation.Id, body, key);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        var result = (await replay.Content.ReadFromJsonAsync<AssistantTurnResultResponse>(AssistantTurnTestSession.Json, Ct))!;
        Assert.Equal(turnId, result.TurnId);
        Assert.Equal(message.Status, result.Message.Status);
        Assert.Equal(message.Text, result.Message.Text);
        Assert.True(result.Usage.Cost.Amount > 0m);
        Assert.Equal(1, session.Provider.Calls);

        using (var scope = session.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var operation = await db.Set<AssistantFinancialOperation>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
            Assert.Equal(AssistantFinancialState.Completed, operation.State);
            var call = await db.Set<AssistantFinancialCall>().SingleAsync(row => row.OperationId == operation.Id && !row.IsTitle, Ct);
            Assert.True(call.IsFinal);
            Assert.True(call.IsEstimated);
            Assert.True(call.Cost.Amount > 0m);
            var quota = await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
            Assert.Equal(1, quota.ConsumedQueries);
            Assert.Equal(0, quota.ReservedQueries);
        }
        foreach (var log in session.Provider.Logs.Snapshot())
        {
            Assert.DoesNotContain(prompt, log, StringComparison.Ordinal);
            Assert.DoesNotContain(AssistantTurnTestProvider.Prefix, log, StringComparison.Ordinal);
            Assert.DoesNotContain(AssistantTurnTestProvider.Suffix, log, StringComparison.Ordinal);
            Assert.DoesNotContain(AssistantTurnTestProvider.SensitiveProviderError, log, StringComparison.Ordinal);
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
