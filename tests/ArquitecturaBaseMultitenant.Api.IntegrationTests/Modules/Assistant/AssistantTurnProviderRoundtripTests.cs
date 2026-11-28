using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantTurnProviderRoundtripTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Accumulated_tool_call_identifiers_are_bounded_before_another_call_is_marked_or_sent()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        session.Provider.ToolKey = "my_account";
        session.Provider.ToolCycles = 4;
        session.Provider.ToolCallIdSize = 16000;
        session.Provider.Release();
        var conversation = await session.ConversationAsync();
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("error", terminal.Event);
        Assert.Equal(AssistantErrors.ProviderContextTooLargeCode, terminal.Data.GetProperty("code").GetString());
        Assert.Equal(2, session.Provider.Calls);
        using var scope = session.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var operation = await db.Set<AssistantFinancialOperation>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(AssistantFinancialState.Completed, operation.State);
        var calls = await db.Set<AssistantFinancialCall>().Where(row => row.OperationId == operation.Id).OrderBy(row => row.Sequence).ToArrayAsync(Ct);
        Assert.Equal(new[] { 1, 2 }, calls.Select(row => row.Sequence));
        Assert.All(calls, call => Assert.True(call.IsFinal));
        var quota = await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(1, quota.ConsumedQueries);
        Assert.Equal(0, quota.ReservedQueries);
    }

    [Fact]
    public async Task Real_anthropic_adapter_preserves_private_thinking_signature_and_redacted_blocks_across_a_real_tool_cycle()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var transport = new ThinkingTransport();
        session.Provider.Transport = transport;
        var conversation = await session.ConversationAsync();
        session.Provider.Logs.Clear();
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        var toolStates = new List<string>();
        AssistantSseTestReader.Frame terminal;
        while (true)
        {
            var frame = await stream.NextAsync(Ct);
            AssertPrivateBlocksAbsent(frame.Data.GetRawText());
            if (frame.Event == "tool") toolStates.Add(frame.Data.GetProperty("state").GetString()!);
            if (frame.Event is "done" or "error") { terminal = frame; break; }
        }
        Assert.Equal("done", terminal.Event);
        Assert.Equal(new[] { "running", "complete" }, toolStates);
        var result = terminal.Data.Deserialize<AssistantTurnResultResponse>(AssistantTurnTestSession.Json)!;
        Assert.Equal(AssistantMessageStatus.Complete, result.Message.Status);
        Assert.Equal(AssistantTurnTestProvider.Answer, result.Message.Text);
        Assert.Equal(2, transport.Requests);
        var bodies = transport.Bodies.ToArray();
        using var second = JsonDocument.Parse(bodies[1]);
        var blocks = second.RootElement.GetProperty("messages").EnumerateArray()
            .Where(message => message.GetProperty("role").GetString() == "assistant")
            .SelectMany(message => message.GetProperty("content").EnumerateArray()).ToArray();
        Assert.Equal(new[] { "thinking", "redacted_thinking", "tool_use" },
            blocks.Select(block => block.GetProperty("type").GetString()));
        Assert.Equal(ThinkingTransport.PrivateThinking, blocks[0].GetProperty("thinking").GetString());
        Assert.Equal(ThinkingTransport.Signature, blocks[0].GetProperty("signature").GetString());
        Assert.Equal(ThinkingTransport.Redacted, blocks[1].GetProperty("data").GetString());
        Assert.Contains("tool_result", second.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(session.Account.Email.Value, second.RootElement.GetRawText(), StringComparison.Ordinal);
        var history = (await session.HistoryAsync(conversation.Id, Ct))!;
        foreach (var message in history.Messages.Items) AssertPrivateBlocksAbsent(message.Text);
        foreach (var log in session.Provider.Logs.Snapshot()) AssertPrivateBlocksAbsent(log);
        using var scope = session.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var operation = await db.Set<AssistantFinancialOperation>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(AssistantFinancialState.Completed, operation.State);
        var calls = await db.Set<AssistantFinancialCall>().Where(row => row.OperationId == operation.Id).ToArrayAsync(Ct);
        Assert.Equal(3, calls.Length);
        Assert.Single(calls, call => call.IsTitle && call.InputTokens == 1 && call.OutputTokens == 1);
        Assert.All(calls.Where(call => !call.IsTitle), call =>
        {
            Assert.Equal(17, call.InputTokens);
            Assert.Equal(7, call.OutputTokens);
            // Sin cache_control no hay escrituras de caché: se cobra la entrada base y no queda marcado como estimado (KAN-73).
            Assert.False(call.IsEstimated);
            var exactCost = decimal.Round((17m * call.Price.Input.Amount + 7m * call.Price.Output.Amount) / 1_000_000m,
                12, MidpointRounding.AwayFromZero);
            Assert.Equal(exactCost, call.Cost.Amount);
            Assert.Equal(call.Price.Input.Currency, call.Cost.Currency);
        });
        var quota = await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(1, quota.ConsumedQueries);
        Assert.Equal(0, quota.ReservedQueries);
    }

    /// <summary>
    /// KAN-75: muchos fragmentos seguidos del proveedor se agrupan en pocos checkpoints en vez de uno por fragmento,
    /// y el lector SSE emite todos los frames nuevos de una misma lectura en orden exacto sin perder texto.
    /// </summary>
    [Fact]
    public async Task Fast_streaming_fragments_are_batched_into_few_checkpoints_and_stream_emits_all_frames_per_read()
    {
        var probe = new TurnServiceProbe();
        await using var session = await AssistantTurnTestSession.CreateAsync(configure: builder =>
            builder.ConfigureTestServices(services =>
            {
                var original = services.Single(row => row.ServiceType == typeof(IAssistantTurnService));
                services.Replace(ServiceDescriptor.Scoped<IAssistantTurnService>(provider =>
                    new ProbedTurnService(
                        (IAssistantTurnService)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!),
                        probe)));
            }));
        var fragments = Enumerable.Range(0, 25)
            .Select(index => "frag-" + index.ToString("D2", CultureInfo.InvariantCulture) + "|")
            .ToArray();
        var expectedText = string.Concat(fragments);
        session.Provider.Transport = new BurstTextTransport(fragments);
        var conversation = await session.ConversationAsync();

        using var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);

        var frames = new List<AssistantSseTestReader.Frame>();
        while (true)
        {
            var frame = await stream.NextAsync(Ct);
            frames.Add(frame);
            if (frame.Event is "done" or "error") break;
        }

        Assert.Equal("start", frames[0].Event);
        var turnId = frames[0].Data.GetProperty("turnId").GetGuid();
        var terminal = frames[^1];
        Assert.Equal("done", terminal.Event);
        var result = terminal.Data.Deserialize<AssistantTurnResultResponse>(AssistantTurnTestSession.Json)!;
        Assert.Equal(AssistantMessageStatus.Complete, result.Message.Status);
        Assert.Equal(expectedText, result.Message.Text);

        var sequences = frames.Select(frame =>
        {
            Assert.NotNull(frame.Id);
            var parts = frame.Id.Split(':');
            Assert.Equal(2, parts.Length);
            Assert.Equal(turnId, Guid.Parse(parts[0]));
            return int.Parse(parts[1], CultureInfo.InvariantCulture);
        }).ToArray();
        for (var index = 1; index < sequences.Length; index++)
            Assert.True(sequences[index] > sequences[index - 1], "SSE frame sequences must be strictly increasing.");

        var streamedDeltas = frames.Where(frame => frame.Event == "delta")
            .Select(frame => frame.Data.GetProperty("text").GetString())
            .ToArray();
        Assert.Equal(expectedText, string.Concat(streamedDeltas));
        Assert.InRange(streamedDeltas.Length, 1, 10);

        // El lector esperó el cierre del turno: todos los frames nuevos salieron en una sola llamada a ReadAsync.
        Assert.True(frames.Count >= 4, $"Expected at least 4 SSE frames (start, deltas, done) but received {frames.Count}.");
        Assert.Equal(1, probe.ReadCalls);
        Assert.InRange(probe.CheckpointCalls, 1, 10);

        using var scope = session.Host.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var turn = await db.Set<AssistantTurn>().SingleAsync(row => row.Id == turnId, Ct);
        var storedDeltas = AssistantTurnJson.Restore<List<AssistantTurnFrame>>(turn.EventsJson)
            .Where(frame => frame.Event == "delta")
            .ToArray();
        Assert.InRange(storedDeltas.Length, 1, 10);
        Assert.Equal(expectedText, string.Concat(storedDeltas.Select(frame => frame.Data.GetProperty("text").GetString())));
    }

    private static void AssertPrivateBlocksAbsent(string value)
    {
        Assert.DoesNotContain(ThinkingTransport.PrivateThinking, value, StringComparison.Ordinal);
        Assert.DoesNotContain(ThinkingTransport.Signature, value, StringComparison.Ordinal);
        Assert.DoesNotContain(ThinkingTransport.Redacted, value, StringComparison.Ordinal);
    }

    private sealed class ThinkingTransport : HttpMessageHandler
    {
        internal const string PrivateThinking = "provider-private-fixture-block";
        internal const string Signature = "opaque-fixture-signature";
        internal const string Redacted = "opaque-redacted-fixture";
        internal ConcurrentQueue<string> Bodies { get; } = new();
        private int requests;
        internal int Requests => Volatile.Read(ref requests);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Enqueue(body);
            using var json = JsonDocument.Parse(body);
            Assert.True(json.RootElement.GetProperty("stream").GetBoolean());
            var first = Interlocked.Increment(ref requests) == 1;
            var events = new StringBuilder();
            Add("message_start", new
            {
                type = "message_start",
                message = new
                {
                    id = first ? "msg_fixture_1" : "msg_fixture_2",
                    type = "message",
                    role = "assistant",
                    model = json.RootElement.GetProperty("model").GetString(),
                    content = Array.Empty<object>(),
                    stop_reason = (string?)null,
                    stop_sequence = (string?)null,
                    usage = new { input_tokens = 17, output_tokens = 1 }
                },
            });
            if (first)
            {
                Add("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "thinking", thinking = "", signature = "" } });
                Add("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "thinking_delta", thinking = PrivateThinking } });
                Add("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "signature_delta", signature = Signature } });
                Add("content_block_stop", new { type = "content_block_stop", index = 0 });
                Add("content_block_start", new { type = "content_block_start", index = 1, content_block = new { type = "redacted_thinking", data = Redacted } });
                Add("content_block_stop", new { type = "content_block_stop", index = 1 });
                Add("content_block_start", new
                {
                    type = "content_block_start",
                    index = 2,
                    content_block = new { type = "tool_use", id = "tool_fixture", name = "my_account", input = new { } }
                });
                Add("content_block_delta", new { type = "content_block_delta", index = 2, delta = new { type = "input_json_delta", partial_json = "{}" } });
                Add("content_block_stop", new { type = "content_block_stop", index = 2 });
            }
            else
            {
                Add("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "text", text = "" } });
                Add("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = AssistantTurnTestProvider.Prefix } });
                Add("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = AssistantTurnTestProvider.Suffix } });
                Add("content_block_stop", new { type = "content_block_stop", index = 0 });
            }
            Add("message_delta", new { type = "message_delta", delta = new { stop_reason = first ? "tool_use" : "end_turn", stop_sequence = (string?)null }, usage = new { output_tokens = 7 } });
            Add("message_stop", new { type = "message_stop" });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(events.ToString(), Encoding.UTF8, "text/event-stream") };

            void Add(string name, object data) => events.Append("event: ").Append(name).Append("\ndata: ")
                .Append(JsonSerializer.Serialize(data)).Append("\n\n");
        }
    }

    private sealed class TurnServiceProbe
    {
        private int _checkpointCalls;
        private int _readCalls;

        internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int CheckpointCalls => Volatile.Read(ref _checkpointCalls);
        internal int ReadCalls => Volatile.Read(ref _readCalls);
        internal void RecordCheckpoint() => Interlocked.Increment(ref _checkpointCalls);
        internal void RecordRead() => Interlocked.Increment(ref _readCalls);
    }

    private sealed class ProbedTurnService(IAssistantTurnService inner, TurnServiceProbe probe) : IAssistantTurnService
    {
        public Task<Result<AssistantTurnExecution>> ClaimAsync(Guid turnId, Guid leaseId, CancellationToken ct) =>
            inner.ClaimAsync(turnId, leaseId, ct);
        public Task<Result> BeginCallAsync(Guid turnId, Guid leaseId, int sequence, CancellationToken ct) =>
            inner.BeginCallAsync(turnId, leaseId, sequence, ct);
        public Task<Result<AssistantTitleCall?>> BeginTitleAsync(Guid turnId, Guid leaseId, CancellationToken ct) =>
            inner.BeginTitleAsync(turnId, leaseId, ct);
        public Task<Result> FinishTitleAsync(Guid turnId, Guid leaseId, uint conversationVersion, string? title, AssistantCallUsage? usage, CancellationToken ct) =>
            inner.FinishTitleAsync(turnId, leaseId, conversationVersion, title, usage, ct);
        public Task<Result> HeartbeatAsync(Guid turnId, Guid leaseId, CancellationToken ct) =>
            inner.HeartbeatAsync(turnId, leaseId, ct);
        public Task<Result> CheckpointAsync(Guid turnId, Guid leaseId, string? delta, AssistantCallUsage? usage, bool callEnded, CancellationToken ct)
        {
            probe.RecordCheckpoint();
            return inner.CheckpointAsync(turnId, leaseId, delta, usage, callEnded, ct);
        }
        public async Task<Result> RecoverAsync(Guid turnId, Guid leaseId, CancellationToken ct)
        {
            try { return await inner.RecoverAsync(turnId, leaseId, ct); }
            finally { probe.Completed.TrySetResult(); }
        }
        public Task<Result> RecordToolAsync(Guid turnId, Guid leaseId, string key, AssistantToolState state, CancellationToken ct) =>
            inner.RecordToolAsync(turnId, leaseId, key, state, ct);
        public Task<Result<AssistantToolExecution>> ExecuteToolAsync(Guid turnId, Guid leaseId, string key, JsonElement arguments, CancellationToken ct) =>
            inner.ExecuteToolAsync(turnId, leaseId, key, arguments, ct);
        public async Task<Result> CompleteAsync(Guid turnId, Guid leaseId, string status, string? code, CancellationToken ct)
        {
            try { return await inner.CompleteAsync(turnId, leaseId, status, code, ct); }
            finally { probe.Completed.TrySetResult(); }
        }
        public Task<Result<bool>> CheckExecutionAsync(Guid turnId, Guid leaseId, CancellationToken ct) =>
            inner.CheckExecutionAsync(turnId, leaseId, ct);
        public async Task<Result<AssistantTurnRead>> ReadAsync(Guid turnId, int afterSequence, CancellationToken ct)
        {
            await probe.Completed.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            probe.RecordRead();
            return await inner.ReadAsync(turnId, afterSequence, ct);
        }
        public Task<Result<IdempotentReplayResponse>> ReplayAsync(IdempotentReference reference, CancellationToken ct) =>
            inner.ReplayAsync(reference, ct);
    }

    private sealed class BurstTextTransport(IReadOnlyList<string> fragments) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            Assert.True(json.RootElement.GetProperty("stream").GetBoolean());
            var events = new StringBuilder();
            Add("message_start", new
            {
                type = "message_start",
                message = new
                {
                    id = "msg_burst_fixture",
                    type = "message",
                    role = "assistant",
                    model = json.RootElement.GetProperty("model").GetString(),
                    content = Array.Empty<object>(),
                    stop_reason = (string?)null,
                    stop_sequence = (string?)null,
                    usage = new { input_tokens = 17, output_tokens = 1 },
                },
            });
            Add("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "text", text = "" } });
            foreach (var fragment in fragments)
                Add("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = fragment } });
            Add("content_block_stop", new { type = "content_block_stop", index = 0 });
            Add("message_delta", new { type = "message_delta", delta = new { stop_reason = "end_turn", stop_sequence = (string?)null }, usage = new { output_tokens = 25 } });
            Add("message_stop", new { type = "message_stop" });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(events.ToString(), Encoding.UTF8, "text/event-stream") };

            void Add(string name, object data) => events.Append("event: ").Append(name).Append("\ndata: ")
                .Append(JsonSerializer.Serialize(data)).Append("\n\n");
        }
    }
}
