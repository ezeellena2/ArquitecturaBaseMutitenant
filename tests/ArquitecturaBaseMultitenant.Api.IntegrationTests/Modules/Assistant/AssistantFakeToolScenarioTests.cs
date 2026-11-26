using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
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

    /// <summary>
    /// I1 de las acciones: el escenario de una acción del modelo falso, como lo usan el recorrido y el E2E en el entorno aislado
    /// (que califica las acciones con cada modelo del catálogo inicial). Propone una vez, la propuesta queda en el mensaje con
    /// su tarjeta, y el texto dice que la preparó; si la fuente la rechaza, no queda propuesta y el texto dice que no pudo.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Action_scenario_proposes_once_and_leaves_the_card_or_says_it_could_not(bool valid)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false);
        var metrics = new AssistantFakeChatMetrics();
        using var host = session.Host.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Assistant:TurnWorkerEnabled", "true");
            builder.ConfigureTestServices(services =>
            {
                FakeChatClient.QualifyActionsWithInitialModels(services);
                services.RemoveAll<IChatClientFactory>();
                services.AddScoped<IChatClientFactory>(provider => new FakeClients(metrics, provider.GetRequiredService<TimeProvider>()));
            });
        });
        using var client = await session.LoginReplicaAsync(host);
        using var create = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/conversations", "{}", Guid.NewGuid());
        using var created = await client.SendAsync(create, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(AssistantTurnTestSession.Json, ct))!;
        var values = valid
            ? new object[] { new { field = "email", value = "ana.lopez@example.test" } }
            : [new { field = "email", value = "ana.lopez@example.test" }, new { field = "roles", value = new[] { "Rol que no existe" } }];
        var text = FakeChatClient.ActionMarker + " " + JsonSerializer.Serialize(new { action = "users.invite@1", values });
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation, text), Guid.NewGuid());
        using var response = await client.SendAsync(send, HttpCompletionOption.ResponseHeadersRead, ct).WaitAsync(TimeSpan.FromSeconds(20), ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, ct);
        AssistantSseTestReader.Frame terminal;
        do terminal = await stream.NextAsync(ct); while (terminal.Event is not ("done" or "error"));

        Assert.True(terminal.Event == "done", terminal.Data.GetRawText());
        Assert.Equal(1, metrics.ToolCycles);
        var message = terminal.Data.GetProperty("message");
        Assert.Equal(valid ? "Te preparé la propuesta. Revisala antes de guardarla." : "No pude prepararla con esos datos. Revisalos y pedila de nuevo.",
            message.GetProperty("text").GetString());
        var proposal = message.TryGetProperty("proposal", out var found) && found.ValueKind == JsonValueKind.Object ? found : (JsonElement?)null;
        Assert.Equal(valid, proposal is not null);
        if (proposal is not { } card) return;
        Assert.Equal("users.invite", card.GetProperty("action").GetString());
        Assert.Equal("new", card.GetProperty("state").GetString());
        using var read = await client.GetAsync($"/api/organization/assistant/proposals/{card.GetProperty("id").GetString()}", ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var body = await read.Content.ReadFromJsonAsync<JsonDocument>(ct);
        var email = Assert.Single(body!.RootElement.GetProperty("values").EnumerateArray());
        Assert.Equal("ana.lopez@example.test", email.GetProperty("value").GetString());
    }

    [Fact]
    public void Action_scenario_written_without_its_action_or_values_is_an_error_of_whoever_wrote_it()
    {
        Assert.Null(FakeChatClient.ActionScenario("[E2E:tool] Consulta de usuarios"));
        Assert.Throws<InvalidOperationException>(() => FakeChatClient.ActionScenario(FakeChatClient.ActionMarker + " {\"values\":[]}"));
        Assert.Throws<InvalidOperationException>(() => FakeChatClient.ActionScenario(FakeChatClient.ActionMarker + " {\"action\":\"users.invite@1\",\"values\":[],\"extra\":1}"));
        var parsed = FakeChatClient.ActionScenario("Quitá a Martín " + FakeChatClient.ActionMarker + " {\"action\":\"users.remove@1\",\"target\":\"Martín Paz\",\"values\":[]}");
        Assert.Equal("users.remove@1", parsed!["action"]);
        Assert.Equal("Martín Paz", parsed["target"]);
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
