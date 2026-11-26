using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// ADR 0068 §6 y §7: sin calificación el turno no ofrece acciones y queda en la versión 1 del motor; con la calificación de
/// prueba del modelo falso las sella, va en la versión 2 y al proveedor le llega la lista precargada como resultado de una
/// herramienta, con ids de acción y nombres, sin registros ni personas.
/// </summary>
public sealed class AssistantActionTurnTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_turn_seals_the_qualified_actions_and_the_provider_gets_only_their_ids_and_names(bool qualified)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false);
        var providerModelId = await DefaultModelAsync(session);
        var seen = new ConcurrentQueue<IReadOnlyList<ChatMessage>>();
        var tools = new ConcurrentQueue<string>();
        using var host = session.Host.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Assistant:TurnWorkerEnabled", "true");
            builder.ConfigureTestServices(services =>
            {
                if (qualified) FakeChatClient.QualifyActions(services, providerModelId);
                services.RemoveAll<IChatClientFactory>();
                services.AddScoped<IChatClientFactory>(_ => new CapturingClients(seen, tools));
            });
        });
        using var client = await session.LoginReplicaAsync(host);
        using var create = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/conversations", "{}", Guid.NewGuid());
        using var created = await client.SendAsync(create, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(AssistantTurnTestSession.Json, Ct))!;
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation, "Invitá a ana@example.com como Vendedora"), Guid.NewGuid());
        using var response = await client.SendAsync(send, HttpCompletionOption.ResponseHeadersRead, Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        AssistantSseTestReader.Frame frame;
        do frame = await stream.NextAsync(Ct); while (frame.Event is not ("done" or "error"));
        Assert.Equal("done", frame.Event);

        using var scope = host.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var turn = await db.Set<AssistantTurn>().AsNoTracking().SingleAsync(row => row.ConversationId == conversation.Id, Ct);
        var work = await db.Set<AssistantTurnWork>().AsNoTracking().SingleAsync(row => row.Id == turn.Id, Ct);
        var request = Assert.Single(seen);
        var preload = request.SelectMany(message => message.Contents).OfType<FunctionResultContent>()
            .SingleOrDefault(content => content.CallId == AssistantActionTools.PreloadCallId);
        if (!qualified)
        {
            Assert.Null(turn.OfferedActions);
            Assert.Equal(1, work.EngineVersion);
            Assert.Null(preload);
            Assert.DoesNotContain(AssistantActionTools.ProposeKey, tools);
            return;
        }
        Assert.Contains("users.invite@1", turn.ReadOfferedActions());
        Assert.Contains("roles.create@1", turn.ReadOfferedActions());
        Assert.Equal(2, work.EngineVersion);
        Assert.Contains(AssistantActionTools.ReadKey, tools);
        Assert.Contains(AssistantActionTools.ProposeKey, tools);
        var sent = Assert.IsType<string>(preload!.Result);
        Assert.Contains("\"action\":\"users.invite@1\"", sent, StringComparison.Ordinal);
        Assert.DoesNotContain(session.Account.UserId.ToString(), sent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(session.TenantId!.Value.ToString(), sent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ana@example.com", sent, StringComparison.Ordinal);
    }

    private static async Task<string> DefaultModelAsync(AssistantTurnTestSession session)
    {
        using var scope = session.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.Set<AssistantPlatformSettings>().AsNoTracking().SingleAsync(Ct);
        return await db.Set<AssistantModelPrice>().AsNoTracking().Where(model => model.Id == settings.DefaultModelId)
            .Select(model => model.ProviderModelId).SingleAsync(Ct);
    }

    private sealed class CapturingClients(ConcurrentQueue<IReadOnlyList<ChatMessage>> seen, ConcurrentQueue<string> tools) : IChatClientFactory
    {
        public Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IChatClient>(new CapturingClient(seen, tools)));
        public Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Key validation is outside the action turn scenario.");
    }

    private sealed class CapturingClient(ConcurrentQueue<IReadOnlyList<ChatMessage>> seen, ConcurrentQueue<string> tools) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Título"))
            { Usage = new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 } });
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            seen.Enqueue(messages.ToArray());
            foreach (var tool in options?.Tools ?? []) tools.Enqueue(tool.Name);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Listo.");
            yield return new ChatResponseUpdate { Contents = [new UsageContent(new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 })], FinishReason = ChatFinishReason.Stop };
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IChatClient) && serviceKey is null ? this : null;
        public void Dispose() { }
    }
}
