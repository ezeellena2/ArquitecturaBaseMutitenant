using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;

/// <summary>Sólo reemplaza IChatClient: API, actor, tools, transacciones y PostgreSQL siguen siendo reales.</summary>
internal sealed class AssistantTurnTestProvider
{
    internal const string Prefix = "Primer fragmento ágil. ";
    internal const string Suffix = "Respuesta terminada.";
    internal const string Answer = Prefix + Suffix;
    internal const int InputTokens = 17;
    internal const int OutputTokens = 7;
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;
    private int _titleCalls;
    internal int Calls => Volatile.Read(ref _calls);
    internal int TitleCalls => Volatile.Read(ref _titleCalls);
    internal bool IsReleased => _release.Task.IsCompleted;
    internal ConcurrentQueue<Invocation> Invocations { get; } = new();
    internal string? ToolKey { get; set; }
    internal int ToolCycles { get; set; } = 1;
    internal int ToolCallIdSize { get; set; }
    internal ConcurrentQueue<string> ToolResults { get; } = new();
    internal ConcurrentQueue<string> Contexts { get; } = new();
    internal bool FailAfterPartial { get; set; }
    internal bool OmitUsage { get; set; }
    internal const string SensitiveProviderError = "isolated-sensitive-provider-payload";
    internal AssistantTurnTestLogCapture Logs { get; } = new();
    internal HttpMessageHandler? Transport { get; set; }
    internal DbCommandInterceptor? DatabaseObserver { get; set; }

    internal void Release() => _release.TrySetResult();
    internal IChatClientFactory Factory(IServiceProvider services) => new Clients(this, services);

    internal sealed record Invocation(Guid? Owner, Access? Access, Guid? TenantId, bool HasHttpRequest, bool HasTransaction);

    private void Record(IServiceProvider services, bool title)
    {
        var user = services.GetRequiredService<ICurrentUser>();
        var tenant = services.GetRequiredService<ITenantContext>();
        Invocations.Enqueue(new(user.UserId, user.Access, tenant.TenantId,
            services.GetRequiredService<IHttpContextAccessor>().HttpContext is not null,
            services.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction is not null));
        if (title) Interlocked.Increment(ref _titleCalls);
        else Interlocked.Increment(ref _calls);
    }

    private sealed class Clients(AssistantTurnTestProvider probe, IServiceProvider services) : IChatClientFactory
    {
        public Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Null(services.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction);
            if (probe.Transport is { } handler)
            {
                var binding = services.GetRequiredService<IAssistantChatContext>().Binding;
                Assert.NotNull(binding);
                return Task.FromResult(Result.Success<IChatClient>(new Client(probe, services, AnthropicChatClientFactory.Build("isolated-roundtrip-key",
                    binding.ProviderModelId, binding.Effort, 2048, new HttpClient(handler)))));
            }
            return Task.FromResult(Result.Success<IChatClient>(new Client(probe, services)));
        }

        public Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class Client(AssistantTurnTestProvider probe, IServiceProvider services, IChatClient? transport = null) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            probe.Record(services, title: true);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Título de prueba"))
            {
                Usage = new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 },
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (transport is not null)
            {
                await foreach (var update in transport.GetStreamingResponseAsync(messages, options, cancellationToken)) yield return update;
                yield break;
            }
            probe.Record(services, title: false);
            probe.Contexts.Enqueue(string.Join("\n", messages.Select(message => message.Text)));
            if (probe.ToolKey is { } toolKey)
            {
                var results = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToArray();
                if (results.Length < probe.ToolCycles)
                {
                    var callId = probe.ToolCallIdSize == 0 ? "isolated-turn-tool"
                        : new string('x', probe.ToolCallIdSize) + "-" + (results.Length + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    yield return new ChatResponseUpdate
                    {
                        Role = ChatRole.Assistant,
                        Contents = [new FunctionCallContent(callId, toolKey, new Dictionary<string, object?>())],
                    };
                    yield return new ChatResponseUpdate
                    {
                        Contents = [new UsageContent(new UsageDetails { InputTokenCount = 3, OutputTokenCount = 2, TotalTokenCount = 5 })],
                        FinishReason = ChatFinishReason.ToolCalls,
                    };
                    yield break;
                }
                foreach (var result in results)
                    probe.ToolResults.Enqueue(result.Result is string text ? text : JsonSerializer.Serialize(result.Result));
            }
            yield return new ChatResponseUpdate(ChatRole.Assistant, Prefix);
            // La primera lectura debe verse antes de liberar al proveedor; no medimos milisegundos.
            await probe._release.Task.WaitAsync(cancellationToken);
            if (probe.FailAfterPartial) throw new HttpRequestException(SensitiveProviderError);
            yield return new ChatResponseUpdate(ChatRole.Assistant, Suffix);
            if (probe.OmitUsage)
            {
                yield return new ChatResponseUpdate { FinishReason = ChatFinishReason.Stop };
                yield break;
            }
            yield return new ChatResponseUpdate
            {
                Contents = [new UsageContent(new UsageDetails
                {
                    InputTokenCount = InputTokens, OutputTokenCount = OutputTokens,
                    TotalTokenCount = InputTokens + OutputTokens,
                })],
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(IChatClient) && serviceKey is null ? this : null;

        public void Dispose() => transport?.Dispose();
    }
}
