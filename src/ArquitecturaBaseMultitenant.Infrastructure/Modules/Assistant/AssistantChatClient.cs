using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using Microsoft.Extensions.AI;
using Anthropic.Exceptions;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Aplica selección y esfuerzo explícitos sin abrir bucles automáticos de herramientas ni reintentos.</summary>
internal sealed class AssistantChatClient(IChatClient inner, IDisposable owner, HttpClient transport, string model, AssistantEffort effort, int maxOutputTokens) : IChatClient
{
    private int disposed;
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        try { return await inner.GetResponseAsync(messages, Prepare(options), cancellationToken); }
        catch (Exception error) when (IsProviderFailure(error, cancellationToken)) { throw Sanitized(error); }
    }
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        StreamAsync(messages, Prepare(options), cancellationToken);
    private async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(IEnumerable<ChatMessage> messages, ChatOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = inner.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool more;
            try { more = await stream.MoveNextAsync(); }
            catch (Exception error) when (IsProviderFailure(error, cancellationToken)) { throw Sanitized(error); }
            if (!more) yield break;
            yield return stream.Current;
        }
    }
    /// <summary>
    /// Lo que viene del proveedor o de su transporte: el SDK, la red, la espera del SDK y una respuesta cortada o ilegible. La
    /// cancelación pedida por quien llama no es una falla.
    /// </summary>
    private static bool IsProviderFailure(Exception error, CancellationToken cancellationToken) => error switch
    {
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        AnthropicException or HttpRequestException or IOException or JsonException => true,
        _ => false,
    };
    /// <summary>
    /// Conserva solo el estado HTTP y la clase de falla del transporte, que <see cref="AssistantProviderFailureClassifier"/>
    /// traduce (0064 §7): sin la excepción original, el cuerpo ni el mensaje del proveedor.
    /// </summary>
    private static HttpRequestException Sanitized(Exception error) => error switch
    {
        AnthropicApiException api => new(HttpRequestError.Unknown, Message, null, api.StatusCode),
        AnthropicIOException { InnerException: HttpRequestException transport } => new(transport.HttpRequestError, Message, null, transport.StatusCode),
        HttpRequestException request => new(request.HttpRequestError, Message, null, request.StatusCode),
        _ => new(HttpRequestError.Unknown, Message),
    };
    private const string Message = "Assistant provider request failed.";
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IChatClient) && serviceKey is null ? this :
        serviceType == typeof(ChatClientMetadata) ? inner.GetService(serviceType, serviceKey) : null;
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try { inner.Dispose(); owner.Dispose(); }
        finally { transport.Dispose(); }
    }
    private ChatOptions Prepare(ChatOptions? supplied)
    {
        var options = supplied?.Clone() ?? new ChatOptions();
        options.ModelId = model;
        options.MaxOutputTokens = Math.Min(options.MaxOutputTokens ?? maxOutputTokens, maxOutputTokens);
        options.Reasoning = new ReasoningOptions { Effort = effort switch { AssistantEffort.Low => ReasoningEffort.Low, AssistantEffort.High => ReasoningEffort.High, _ => ReasoningEffort.Medium } };
        return options;
    }
}
