using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using Microsoft.Extensions.AI;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Doble determinista disponible únicamente mediante opciones de host validadas.</summary>
internal sealed class FakeChatClient(AssistantFakeChatMetrics metrics, TimeProvider clock) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        metrics.Title();
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, AssistantTexts.Get("Assistant.Fake.Title")))
        { Usage = new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 } });
    }
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        metrics.Call();
        var input = messages.ToArray();
        var scenario = input.LastOrDefault(row => row.Role == ChatRole.User)?.Text ?? string.Empty;
        if (scenario.Contains("[E2E:tool]", StringComparison.Ordinal)
            && !input.SelectMany(row => row.Contents).OfType<FunctionResultContent>().Any())
        {
            var tool = options?.Tools?.SingleOrDefault(row => row.Name == "org_users")
                ?? throw new InvalidOperationException("The scripted users scenario requires its declared tool.");
            metrics.Tool();
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new FunctionCallContent("e2e-read-only", tool.Name, new Dictionary<string, object?> { ["page"] = 1, ["pageSize"] = 20 })],
            };
            yield return new ChatResponseUpdate { Contents = [new UsageContent(new UsageDetails { InputTokenCount = 3, OutputTokenCount = 2, TotalTokenCount = 5 })], FinishReason = ChatFinishReason.ToolCalls };
            yield break;
        }
        yield return new ChatResponseUpdate(ChatRole.Assistant, AssistantTexts.Get("Assistant.Fake.Prefix"));
        await Task.Delay(scenario.Contains("[E2E:slow]", StringComparison.Ordinal) ? TimeSpan.FromSeconds(3) : TimeSpan.FromMilliseconds(200), clock, cancellationToken);
        if (scenario.Contains("[E2E:error]", StringComparison.Ordinal)) throw new HttpRequestException("Isolated provider failure.");
        yield return new ChatResponseUpdate(ChatRole.Assistant, AssistantTexts.Get("Assistant.Fake.Suffix"));
        yield return new ChatResponseUpdate { Contents = [new UsageContent(new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 })], FinishReason = ChatFinishReason.Stop };
    }
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IChatClient) && serviceKey is null ? this : null;
    public void Dispose() { }
}
