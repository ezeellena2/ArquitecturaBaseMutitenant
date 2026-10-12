using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantIdempotencyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Catalog_price_changes_after_admission_do_not_reprice_the_inflight_call_or_its_replay()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        var body = AssistantTurnTestSession.Body(conversation);
        var key = Guid.NewGuid();
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", body, key);
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        Assert.Equal("start", (await stream.NextAsync(Ct)).Event);
        Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);
        AssistantPriceSnapshot admitted;
        using (var read = session.Host.Services.CreateScope())
            admitted = (await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantFinancialOperation>()
                .AsNoTracking().SingleAsync(row => row.UserId == session.Account.UserId, Ct)).Price;
        using (var edit = session.Host.Services.CreateScope())
        {
            var services = edit.ServiceProvider;
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var model = await services.GetRequiredService<ApplicationDbContext>().Set<AssistantModelPrice>()
                    .SingleAsync(row => row.Id == admitted.ModelId, ct);
                model.Update(model.Provider, model.ProviderModelId, model.DisplayName,
                    new Money(777m, admitted.Input.Currency), new Money(888m, admitted.Output.Currency), true);
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(
                    AuditActorKind.System, Guid.Empty, "Changed live tariff after turn admission",
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
        }
        session.Provider.Release();
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("done", terminal.Event);
        using (var read = session.Host.Services.CreateScope())
        {
            var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var operation = await db.Set<AssistantFinancialOperation>().AsNoTracking()
                .SingleAsync(row => row.UserId == session.Account.UserId, Ct);
            var call = await db.Set<AssistantFinancialCall>().AsNoTracking().SingleAsync(row => row.OperationId == operation.Id && !row.IsTitle, Ct);
            Assert.Equal(admitted.Input, operation.Price.Input);
            Assert.Equal(admitted.Output, operation.Price.Output);
            Assert.Equal(admitted.Input, call.Price.Input);
            Assert.Equal(admitted.Output, call.Price.Output);
            Assert.Equal(admitted.Revision, call.Price.Revision);
            var live = await db.Set<AssistantModelPrice>().AsNoTracking().SingleAsync(row => row.Id == admitted.ModelId, Ct);
            Assert.Equal(777m, live.InputPrice.Amount);
            Assert.Equal(888m, live.OutputPrice.Amount);
        }
        using var replay = await AssistantStreamingTests.CompletedReplayAsync(session, conversation.Id, body, key);
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct));
        Assert.True(JsonElement.DeepEquals(terminal.Data, replayJson.RootElement));
        Assert.Equal(1, session.Provider.Calls);
    }

    [Fact]
    public async Task Terminal_replay_survives_global_expiry_without_provider_calls_or_private_content_in_the_global_store()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        var body = AssistantTurnTestSession.Body(conversation, "Consulta privada: jamás en la tabla global");
        var key = Guid.NewGuid();
        var path = session.Path + $"/conversations/{conversation.Id:D}/messages";
        session.Provider.Release();
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post, path, body, key);
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("done", terminal.Event);

        var calls = session.Provider.Calls;
        var titleCalls = session.Provider.TitleCalls;
        Assert.Equal(1, calls);
        using (var replay = await AssistantStreamingTests.CompletedReplayAsync(session, conversation.Id, body, key))
        {
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
            using var result = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct));
            Assert.True(JsonElement.DeepEquals(terminal.Data, result.RootElement));
        }
        using (var scope = session.Host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var global = await db.Set<IdempotencyKey>().SingleAsync(row => row.UserId == session.Account.UserId && row.Key == key, ct);
                Assert.Null(global.ResponseBody);
                Assert.NotNull(global.ReferenceKind);
                Assert.NotNull(global.ReferenceId);
                Assert.Equal(200, global.ResponseStatusCode);
                Assert.DoesNotContain("Consulta privada", global.BodyHash, StringComparison.Ordinal);
                Assert.DoesNotContain(AssistantTurnTestProvider.Prefix, global.Route, StringComparison.Ordinal);
                db.Entry(global).Property(row => row.ExpiresAtUtc).CurrentValue =
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(-2);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
        }
        using (var afterExpiry = await AssistantStreamingTests.CompletedReplayAsync(session, conversation.Id, body, key))
        {
            using var result = JsonDocument.Parse(await afterExpiry.Content.ReadAsStringAsync(Ct));
            Assert.True(JsonElement.DeepEquals(terminal.Data, result.RootElement));
        }
        using (var changed = AssistantTurnTestSession.Request(HttpMethod.Post, path,
            AssistantTurnTestSession.Body(conversation, "Otro cuerpo"), key))
        using (var conflict = await session.Client.SendAsync(changed, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(conflict, HttpStatusCode.UnprocessableEntity);

        using (var scope = session.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var quota = await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
            Assert.Equal(1, quota.ConsumedQueries);
            Assert.Equal(0, quota.ReservedQueries);
            var operations = await db.Set<AssistantFinancialOperation>().Where(row => row.UserId == session.Account.UserId).ToArrayAsync(Ct);
            Assert.Equal(AssistantFinancialState.Completed, Assert.Single(operations).State);
        }
        Assert.Equal(calls, session.Provider.Calls);
        Assert.Equal(titleCalls, session.Provider.TitleCalls);
    }

    [Fact]
    public async Task Two_hosts_arbitrate_the_active_key_body_and_stop_without_repeating_a_provider_effect()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        using var replica = session.Replica();
        using var secondClient = await session.LoginReplicaAsync(replica);
        var conversation = await session.ConversationAsync();
        var body = AssistantTurnTestSession.Body(conversation);
        var key = Guid.NewGuid();
        var path = session.Path + $"/conversations/{conversation.Id:D}/messages";
        using var first = AssistantTurnTestSession.Request(HttpMethod.Post, path, body, key);
        using var response = await session.OpenStreamAsync(first, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var start = await stream.NextAsync(Ct);
        Assert.Equal("start", start.Event);
        var turnId = start.Data.GetProperty("turnId").GetGuid();
        Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);

        using (var same = AssistantTurnTestSession.Request(HttpMethod.Post, path, body, key))
        using (var inProgress = await secondClient.SendAsync(same, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(inProgress, HttpStatusCode.Conflict);
        using (var different = AssistantTurnTestSession.Request(HttpMethod.Post, path,
            AssistantTurnTestSession.Body(conversation, "Cuerpo diferente mientras está activo"), key))
        using (var reused = await secondClient.SendAsync(different, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(reused, HttpStatusCode.UnprocessableEntity);

        // Otra key tampoco permite un segundo turno en la conversación, aunque tenga la versión vigente.
        var history = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(history);
        using (var otherKey = AssistantTurnTestSession.Request(HttpMethod.Post, path,
            AssistantTurnTestSession.Body(history.Conversation), Guid.NewGuid()))
        using (var busy = await secondClient.SendAsync(otherKey, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(busy, HttpStatusCode.Conflict, AssistantErrors.TurnInProgressCode);

        using (var stop = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/turns/{turnId:D}/stop", "{}", Guid.NewGuid()))
        using (var stopped = await secondClient.SendAsync(stop, Ct))
            Assert.Equal(HttpStatusCode.NoContent, stopped.StatusCode);
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("done", terminal.Event);
        Assert.Equal("Stopped", terminal.Data.GetProperty("message").GetProperty("status").GetString());
        Assert.False(session.Provider.IsReleased);
        using var replayRequest = AssistantTurnTestSession.Request(HttpMethod.Post, path, body, key);
        using var replay = await secondClient.SendAsync(replayRequest, Ct);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("application/json", replay.Content.Headers.ContentType?.MediaType);
        using var result = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct));
        Assert.True(JsonElement.DeepEquals(terminal.Data, result.RootElement));
        Assert.Equal(1, session.Provider.Calls);
    }

    [Fact]
    public async Task Replay_rechecks_the_notice_and_deleted_conversation_keeps_a_non_repeatable_technical_binding()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        var key = Guid.NewGuid();
        var body = AssistantTurnTestSession.Body(conversation);
        var path = session.Path + $"/conversations/{conversation.Id:D}/messages";
        session.Provider.Release();
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, path, body, key);
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        Assert.Equal("done", (await stream.TerminalAsync(Ct)).Event);
        var calls = session.Provider.Calls;
        using (var scope = session.Host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.RemoveRange(await db.Set<AssistantNoticeAcceptance>().Where(row => row.OwnerUserId == session.Account.UserId).ToArrayAsync(ct));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
        }
        using (var repeated = AssistantTurnTestSession.Request(HttpMethod.Post, path, body, key))
        using (var denied = await session.Client.SendAsync(repeated, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(denied, HttpStatusCode.BadRequest, AssistantErrors.NoticeAcceptanceRequiredCode);
        await AssistantExecutionActorTests.AcceptNoticeAsync(session.Client);
        var current = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(current);
        using (var deleted = await session.Client.DeleteAsync(session.Path + $"/conversations/{conversation.Id:D}?version={current.Conversation.Version}", Ct))
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using (var repeated = AssistantTurnTestSession.Request(HttpMethod.Post, path, body, key))
        using (var gone = await session.Client.SendAsync(repeated, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(gone, HttpStatusCode.NotFound);
        using (var changed = AssistantTurnTestSession.Request(HttpMethod.Post, path, AssistantTurnTestSession.Body(conversation, "Otro cuerpo"), key))
        using (var reused = await session.Client.SendAsync(changed, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(reused, HttpStatusCode.UnprocessableEntity);
        Assert.Equal(calls, session.Provider.Calls);
    }
}
