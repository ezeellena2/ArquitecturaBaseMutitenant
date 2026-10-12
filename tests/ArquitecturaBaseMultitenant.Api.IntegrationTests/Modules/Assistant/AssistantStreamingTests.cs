using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantStreamingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("consumer", Access.Consumer)]
    [InlineData("business", Access.Business)]
    [InlineData("platform", Access.Platform)]
    public async Task First_delta_is_flushed_before_completion_and_the_worker_uses_the_admitted_actor(
        string access, Access expectedAccess)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync(access);
        var conversation = await session.ConversationAsync();
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);

        var start = await stream.NextAsync(Ct);
        Assert.Equal("start", start.Event);
        var turnId = start.Data.GetProperty("turnId").GetGuid();
        var userMessageId = start.Data.GetProperty("userMessageId").GetGuid();
        var assistantMessageId = start.Data.GetProperty("assistantMessageId").GetGuid();
        Assert.NotEqual(Guid.Empty, turnId);
        Assert.NotEqual(userMessageId, assistantMessageId);
        Assert.NotEqual(conversation.Version, start.Data.GetProperty("conversationVersion").GetUInt32());
        var delta = await stream.NextAsync(Ct);
        Assert.Equal("delta", delta.Event);
        Assert.Equal(turnId, delta.Data.GetProperty("turnId").GetGuid());
        Assert.Equal(AssistantTurnTestProvider.Prefix, delta.Data.GetProperty("text").GetString());
        Assert.True(delta.Data.GetProperty("sequence").GetInt32() > 0);
        Assert.False(session.Provider.IsReleased);
        Assert.Equal(1, session.Provider.Calls);
        AssertFrameIds(start, delta, turnId);

        var generating = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(generating);
        Assert.Equal(turnId, generating.ActiveTurnId);
        Assert.Equal(2, generating.Messages.Items.Count);
        Assert.Equal(AssistantMessageRole.User, generating.Messages.Items.Single(row => row.Id == userMessageId).Role);
        var partial = generating.Messages.Items.Single(row => row.Id == assistantMessageId);
        Assert.Equal(AssistantMessageStatus.Generating, partial.Status);
        Assert.Equal(AssistantTurnTestProvider.Prefix, partial.Text);
        var actor = Assert.Single(session.Provider.Invocations);
        Assert.Equal(session.Account.UserId, actor.Owner);
        Assert.Equal(expectedAccess, actor.Access);
        Assert.Equal(session.TenantId, actor.TenantId);
        Assert.False(actor.HasHttpRequest);
        Assert.False(actor.HasTransaction);

        session.Provider.Release();
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("done", terminal.Event);
        var result = terminal.Data.Deserialize<AssistantTurnResultResponse>(AssistantTurnTestSession.Json)!;
        Assert.Equal(turnId, result.TurnId);
        Assert.Equal(assistantMessageId, result.Message.Id);
        Assert.Equal(AssistantMessageStatus.Complete, result.Message.Status);
        Assert.Equal(AssistantTurnTestProvider.Answer, result.Message.Text);
        Assert.True(result.Usage.InputTokens >= AssistantTurnTestProvider.InputTokens);
        Assert.True(result.Usage.OutputTokens >= AssistantTurnTestProvider.OutputTokens);
        Assert.True(result.Usage.Cost.Amount > 0m);
        Assert.Equal("USD", result.Usage.Cost.Currency.Value);
        var finished = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(finished);
        Assert.Null(finished.ActiveTurnId);
        Assert.Equal(AssistantMessageStatus.Complete, finished.Messages.Items.Single(row => row.Id == assistantMessageId).Status);
        Assert.Equal(1, session.Provider.Calls);
        Assert.All(session.Provider.Invocations, invocation =>
        {
            Assert.False(invocation.HasHttpRequest);
            Assert.False(invocation.HasTransaction);
        });
    }

    [Fact]
    public async Task Disconnecting_the_reader_keeps_the_turn_running_and_reopening_recovers_its_completion()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        var key = Guid.NewGuid();
        var body = AssistantTurnTestSession.Body(conversation);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", body, key);
        var response = await session.OpenStreamAsync(request, cancellation.Token);
        using (response)
        using (var stream = await AssistantSseTestReader.OpenAsync(response, cancellation.Token))
        {
            var start = await stream.NextAsync(Ct);
            Assert.Equal("start", start.Event);
            var turnId = start.Data.GetProperty("turnId").GetGuid();
            Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);
            cancellation.Cancel();
            var reopened = await session.HistoryAsync(conversation.Id, Ct);
            Assert.NotNull(reopened);
            Assert.Equal(turnId, reopened.ActiveTurnId);
            Assert.Contains(reopened.Messages.Items, row => row.Status == AssistantMessageStatus.Generating);
            Assert.False(session.Provider.IsReleased);
        }

        session.Provider.Release();
        var replay = await CompletedReplayAsync(session, conversation.Id, body, key);
        using (replay)
        {
            Assert.Equal("application/json", replay.Content.Headers.ContentType?.MediaType);
            var result = (await replay.Content.ReadFromJsonAsync<AssistantTurnResultResponse>(AssistantTurnTestSession.Json, Ct))!;
            Assert.Equal(AssistantMessageStatus.Complete, result.Message.Status);
            Assert.Equal(AssistantTurnTestProvider.Answer, result.Message.Text);
        }
        Assert.Equal(1, session.Provider.Calls);
    }

    [Fact]
    public async Task Explicit_stop_is_durable_idempotent_and_preserves_the_partial_and_accounting()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        var key = Guid.NewGuid();
        var body = AssistantTurnTestSession.Body(conversation);
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", body, key);
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var start = await stream.NextAsync(Ct);
        var turnId = start.Data.GetProperty("turnId").GetGuid();
        Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);
        var stopKey = Guid.NewGuid();
        var stopPath = session.Path + $"/conversations/{conversation.Id:D}/turns/{turnId:D}/stop";
        for (var index = 0; index < 2; index++)
        {
            using var stop = AssistantTurnTestSession.Request(HttpMethod.Post, stopPath, "{}", stopKey);
            using var stopped = await session.Client.SendAsync(stop, Ct);
            Assert.Equal(HttpStatusCode.NoContent, stopped.StatusCode);
        }
        // El proveedor permanece bloqueado: Detener debe cancelar la ejecución, sin liberar el latch del test.
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("done", terminal.Event);
        var result = terminal.Data.Deserialize<AssistantTurnResultResponse>(AssistantTurnTestSession.Json)!;
        Assert.Equal(turnId, result.TurnId);
        Assert.Equal(AssistantMessageStatus.Stopped, result.Message.Status);
        Assert.Equal(AssistantTurnTestProvider.Prefix, result.Message.Text);
        Assert.True(result.Usage.Cost.Amount > 0m);
        Assert.False(session.Provider.IsReleased);
        var history = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(history);
        Assert.Null(history.ActiveTurnId);
        Assert.Equal(AssistantMessageStatus.Stopped, history.Messages.Items.Single(row => row.Id == result.Message.Id).Status);
        using var replay = await CompletedReplayAsync(session, conversation.Id, body, key);
        var original = terminal.Data;
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct));
        Assert.True(JsonElement.DeepEquals(original, replayJson.RootElement));
        Assert.Equal(1, session.Provider.Calls);
    }

    internal static async Task<HttpResponseMessage> CompletedReplayAsync(AssistantTurnTestSession session,
        Guid conversationId, string body, Guid key)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
                session.Path + $"/conversations/{conversationId:D}/messages", body, key);
            var response = await session.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.Conflict)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
                return response;
            }
            response.Dispose();
            await Task.Delay(25, deadline.Token);
        }
    }

    private static void AssertFrameIds(AssistantSseTestReader.Frame start, AssistantSseTestReader.Frame delta, Guid turnId)
    {
        Assert.NotNull(start.Id);
        Assert.NotNull(delta.Id);
        var first = start.Id.Split(':');
        var second = delta.Id.Split(':');
        Assert.Equal(2, first.Length);
        Assert.Equal(2, second.Length);
        Assert.Equal(turnId, Guid.Parse(first[0]));
        Assert.Equal(turnId, Guid.Parse(second[0]));
        Assert.True(int.Parse(second[1], System.Globalization.CultureInfo.InvariantCulture)
            > int.Parse(first[1], System.Globalization.CultureInfo.InvariantCulture));
    }
}
