using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantTitleTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task First_complete_answer_generates_one_title_without_an_extra_query_and_replay_does_not_call_again()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        session.Provider.Release();
        var conversation = await session.ConversationAsync();
        var key = Guid.NewGuid();
        var body = AssistantTurnTestSession.Body(conversation);
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", body, key);
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var titles = new List<AssistantSseTestReader.Frame>();
        while (true)
        {
            var frame = await stream.NextAsync(Ct);
            if (frame.Event == "title") titles.Add(frame);
            if (frame.Event is "done" or "error") { Assert.Equal("done", frame.Event); break; }
        }
        Assert.Equal("Título de prueba", Assert.Single(titles).Data.GetProperty("title").GetString());
        Assert.Equal(1, session.Provider.TitleCalls);
        Assert.Equal(1, session.Provider.Calls);
        var history = await session.HistoryAsync(conversation.Id, Ct);
        Assert.Equal("Título de prueba", history!.Conversation.Title);
        Assert.Equal(2, history.Messages.Items.Count);
        using var replay = await AssistantStreamingTests.CompletedReplayAsync(session, conversation.Id, body, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, session.Provider.TitleCalls);
        using var scope = session.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var operation = await db.Set<AssistantFinancialOperation>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        var calls = await db.Set<AssistantFinancialCall>().Where(row => row.OperationId == operation.Id).ToArrayAsync(Ct);
        Assert.Equal(2, calls.Length);
        Assert.Single(calls, row => row.IsTitle && row.OutputTokens == 1 && row.IsFinal);
        var quota = await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(1, quota.ConsumedQueries);
        Assert.Equal(0, quota.ReservedQueries);
        using var renamed = await session.Client.PutAsJsonAsync(session.Path + $"/conversations/{conversation.Id:D}",
            new { title = "Nombre posterior", isSaved = true, version = history.Conversation.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        using var lateScope = session.Host.Services.CreateScope();
        var late = lateScope.ServiceProvider;
        var work = await late.GetRequiredService<ApplicationDbContext>().Set<AssistantTurnWork>().AsNoTracking().SingleAsync(row => row.Id == operation.Id, Ct);
        var actor = await late.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(new ExecutionActor(work.OwnerUserId, work.Access,
            work.ScopeTenantId, work.Culture, work.TimeZoneId) { TenantKind = work.TenantKind, SessionStartedAtUtc = work.SessionStartedAtUtc }, Ct);
        Assert.True(actor.IsSuccess, actor.Error.Code);
        using var entered = late.GetRequiredService<IExecutionActorScope>().Enter(actor.Value);
        var lateFrames = await late.GetRequiredService<IAssistantTurnService>().ReadAsync(work.Id, -1, Ct);
        Assert.True(lateFrames.IsSuccess, lateFrames.Error.Code);
        Assert.DoesNotContain(lateFrames.Value.Frames, frame => frame.Event == "title");
    }

    [Fact]
    public async Task A_manual_rename_while_the_answer_is_streaming_prevents_the_title_call_and_preserves_the_name()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        Assert.Equal("start", (await stream.NextAsync(Ct)).Event);
        Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);
        var history = await session.HistoryAsync(conversation.Id, Ct);
        using var renamed = await session.Client.PutAsJsonAsync(session.Path + $"/conversations/{conversation.Id:D}",
            new { title = "Nombre elegido", isSaved = true, version = history!.Conversation.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        session.Provider.Release();
        Assert.Equal("done", (await stream.TerminalAsync(Ct)).Event);
        Assert.Equal(0, session.Provider.TitleCalls);
        Assert.Equal("Nombre elegido", (await session.HistoryAsync(conversation.Id, Ct))!.Conversation.Title);
    }
}
