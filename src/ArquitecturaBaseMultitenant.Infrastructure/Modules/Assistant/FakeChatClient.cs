using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using Microsoft.Extensions.AI;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Doble determinista disponible únicamente mediante opciones de host validadas.</summary>
internal sealed class FakeChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, AssistantTexts.Get("Assistant.Fake.Response")))
        { Usage = new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 } });
    }
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;
        yield return new ChatResponseUpdate(ChatRole.Assistant, AssistantTexts.Get("Assistant.Fake.Response"));
        yield return new ChatResponseUpdate { Contents = [new UsageContent(new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 })], FinishReason = ChatFinishReason.Stop };
    }
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IChatClient) && serviceKey is null ? this : null;
    public void Dispose() { }
}
