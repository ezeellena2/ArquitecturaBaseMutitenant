using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantFakeToolScenarioTests
{
    [Fact]
    public async Task Tool_and_slow_scenario_executes_real_users_dispatcher_and_publishes_durable_completion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false);
        var metrics = new AssistantFakeChatMetrics();
        using var host = session.Host.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Assistant:TurnWorkerEnabled", "true");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClientFactory>();
                services.AddScoped<IChatClientFactory>(provider => new FakeClients(metrics, provider.GetRequiredService<TimeProvider>()));
            });
        });
        using var client = await session.LoginReplicaAsync(host);
        using var create = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/conversations", "{}", Guid.NewGuid());
        using var created = await client.SendAsync(create, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(AssistantTurnTestSession.Json, ct))!;
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation, "[E2E:tool] [E2E:slow] Consulta de usuarios"), Guid.NewGuid());
        using var response = await client.SendAsync(send, HttpCompletionOption.ResponseHeadersRead, ct).WaitAsync(TimeSpan.FromSeconds(20), ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, ct);
        var states = new List<string>();
        var deltas = new List<string>();
        AssistantSseTestReader.Frame terminal;
        while (true)
        {
            var frame = await stream.NextAsync(ct);
            if (frame.Event == "tool")
            {
                Assert.Equal("org_users", frame.Data.GetProperty("toolKey").GetString());
                Assert.Equal("Tool.org_users", frame.Data.GetProperty("labelKey").GetString());
                states.Add(frame.Data.GetProperty("state").GetString()!);
                Assert.False(frame.Data.TryGetProperty("arguments", out _));
            }
            if (frame.Event == "delta") deltas.Add(frame.Data.GetProperty("text").GetString()!);
            if (frame.Event is "done" or "error") { terminal = frame; break; }
        }
        Assert.Equal("done", terminal.Event);
        Assert.Equal(new[] { "running", "complete" }, states);
        Assert.Equal(2, deltas.Count);
        Assert.Equal(string.Concat(deltas), terminal.Data.GetProperty("message").GetProperty("text").GetString());
        Assert.Equal(2, metrics.Calls);
        Assert.Equal(1, metrics.ToolCycles);
        Assert.Equal(1, metrics.TitleCalls);
    }

    [Fact]
    public async Task Tool_scenario_without_users_declaration_cannot_silently_call_a_different_tool()
    {
        var ct = TestContext.Current.CancellationToken;
        var metrics = new AssistantFakeChatMetrics();
        using var client = new FakeChatClient(metrics, TimeProvider.System);
        var other = AIFunctionFactory.CreateDeclaration("my_account", "Read account", JsonSerializer.SerializeToElement(new { type = "object" }));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "[E2E:tool]")],
                new ChatOptions { Tools = [other] }, ct)) { }
        });
        Assert.Equal(0, metrics.ToolCycles);
    }

    private sealed class FakeClients(AssistantFakeChatMetrics metrics, TimeProvider clock) : IChatClientFactory
    {
        public Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success<IChatClient>(new FakeChatClient(metrics, clock)));
        }
        public Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Key validation is outside the scripted tool scenario.");
    }
}
