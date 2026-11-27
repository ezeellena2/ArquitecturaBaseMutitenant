using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

    /// <summary>
    /// 0068 §7 (0064 §9 y §10): el mensaje con una propuesta lleva en el pedido siguiente la acción y lo que escribió el modelo,
    /// como la llamada aceptada; nunca el id resuelto, el destino resuelto ni un nombre leído del servidor.
    /// </summary>
    [Fact]
    public async Task The_next_turn_carries_the_proposal_as_written_without_resolved_ids_or_names()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false);
        var seen = new ConcurrentQueue<IReadOnlyList<ChatMessage>>();
        using var host = session.Host.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Assistant:TurnWorkerEnabled", "true");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClientFactory>();
                services.AddScoped<IChatClientFactory>(_ => new CapturingClients(seen, new ConcurrentQueue<string>()));
            });
        });
        using var client = await session.LoginReplicaAsync(host);
        using var create = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/conversations", "{}", Guid.NewGuid());
        using var created = await client.SendAsync(create, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(AssistantTurnTestSession.Json, Ct))!;
        var resolvedTarget = Guid.NewGuid();
        var resolvedPermission = Guid.NewGuid().ToString("D");
        using (var scope = session.Factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
            var asked = AssistantMessage.Create(conversation.Id, "User", "Complete", "Prepará el rol de ventas",
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddMinutes(-2));
            var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().Add(asked);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(saved.IsSuccess, saved.Error.Code);
        }
        await AssistantTestData.ProposalAsync(session.Factory, session.TenantId!.Value, conversation.Id, session.Account.UserId, "roles.edit", 1,
            new AssistantProposalValues("Rol de ventas", [AssistantTestData.Written("permissions", "ver usuarios", resolvedPermission)]),
            resolvedTarget, 7);
        var history = await session.HistoryAsync(conversation.Id, Ct);
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            JsonSerializer.Serialize(new { text = "Sumale también ver roles", attachmentIds = Array.Empty<Guid>(), conversationVersion = history!.Conversation.Version }),
            Guid.NewGuid());
        using var response = await client.SendAsync(send, HttpCompletionOption.ResponseHeadersRead, Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        AssistantSseTestReader.Frame frame;
        do frame = await stream.NextAsync(Ct); while (frame.Event is not ("done" or "error"));
        Assert.Equal("done", frame.Event);

        var request = Assert.Single(seen);
        var texts = request.SelectMany(message => message.Contents).OfType<TextContent>().Select(content => content.Text).ToArray();
        var written = Assert.Single(texts, text => text.StartsWith("{\"propose_action\":", StringComparison.Ordinal));
        using var proposal = JsonDocument.Parse(written);
        Assert.Equal("accepted", proposal.RootElement.GetProperty("outcome").GetString());
        var call = proposal.RootElement.GetProperty("propose_action");
        Assert.Equal("roles.edit@1", call.GetProperty("action").GetString());
        Assert.Equal("Rol de ventas", call.GetProperty("target").GetString());
        var value = Assert.Single(call.GetProperty("values").EnumerateArray());
        Assert.Equal("permissions", value.GetProperty("field").GetString());
        Assert.Equal("ver usuarios", value.GetProperty("value").GetString());
        Assert.Equal(new[] { "action", "target", "values" }, call.EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "field", "value" }, value.EnumerateObject().Select(property => property.Name));
        var everything = string.Join(' ', texts);
        Assert.DoesNotContain(resolvedTarget.ToString(), everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(resolvedPermission, everything, StringComparison.OrdinalIgnoreCase);
        // Va en el mensaje del asistente que la propuso, después de la pregunta.
        var holder = Assert.Single(request, message => message.Contents.OfType<TextContent>().Any(content => content.Text == written));
        Assert.Equal(ChatRole.Assistant, holder.Role);
    }

    /// <summary>
    /// Con las acciones selladas el turno suma la lista precargada y sus tres herramientas; cinco imágenes chicas, el máximo de
    /// un mensaje, igual entran en el contexto y el turno termina en done, no en <c>Assistant.Provider.ContextTooLarge</c>.
    /// </summary>
    [Fact]
    public async Task Five_small_images_with_the_actions_offered_fit_the_context_and_the_turn_ends_done()
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
                FakeChatClient.QualifyActions(services, providerModelId);
                services.RemoveAll<IChatClientFactory>();
                services.AddScoped<IChatClientFactory>(_ => new CapturingClients(seen, tools));
            });
        });
        using var client = await session.LoginReplicaAsync(host);
        using var create = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/conversations", "{}", Guid.NewGuid());
        using var created = await client.SendAsync(create, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(AssistantTurnTestSession.Json, Ct))!;
        var version = conversation.Version;
        var attachments = new List<Guid>();
        for (var index = 0; index < AssistantTechnicalLimits.MaxAttachments; index++)
        {
            using var uploaded = await AssistantAttachmentsTests.UploadAsync(client, conversation.Id, version, Guid.NewGuid(), SmallPng, "image/png",
                root: session.Path);
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
            attachments.Add((await uploaded.Content.ReadFromJsonAsync<AssistantAttachmentSummaryResponse>(AssistantTurnTestSession.Json, Ct))!.Id);
            version = (await client.GetFromJsonAsync<AssistantConversationResponse>(session.Path + $"/conversations/{conversation.Id:D}",
                AssistantTurnTestSession.Json, Ct))!.Conversation.Version;
        }
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            JsonSerializer.Serialize(new { text = "Cinco imágenes de prueba.", attachmentIds = attachments, conversationVersion = version }), Guid.NewGuid());
        using var response = await client.SendAsync(send, HttpCompletionOption.ResponseHeadersRead, Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        AssistantSseTestReader.Frame frame;
        do frame = await stream.NextAsync(Ct); while (frame.Event is not ("done" or "error"));
        Assert.True(frame.Event == "done", frame.Data.ToString());

        var request = Assert.Single(seen);
        Assert.Equal(AssistantTechnicalLimits.MaxAttachments, request.SelectMany(message => message.Contents).OfType<DataContent>().Count());
        Assert.Contains(request.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == AssistantActionTools.PreloadCallId);
        Assert.Contains(AssistantActionTools.ProposeKey, tools);
    }

    // El PNG de 1x1 que sube el E2E real.
    private static readonly byte[] SmallPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jh5kAAAAASUVORK5CYII=");

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
