using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using Microsoft.Extensions.AI;
using Anthropic.Exceptions;
using System.Net;
using System.Runtime.CompilerServices;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Aplica selección y esfuerzo explícitos sin abrir bucles automáticos de herramientas ni reintentos.</summary>
internal sealed class AssistantChatClient(IChatClient inner, IDisposable owner, HttpClient transport, string model, AssistantEffort effort, int maxOutputTokens) : IChatClient
{
    private int disposed;
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        try { return await inner.GetResponseAsync(messages, Prepare(options), cancellationToken); }
        catch (AnthropicException error) { throw Sanitized(error); }
        catch (HttpRequestException error) { throw Sanitized(error); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw Sanitized(null); }
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
            catch (AnthropicException error) { throw Sanitized(error); }
            catch (HttpRequestException error) { throw Sanitized(error); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw Sanitized(null); }
            if (!more) yield break;
            yield return stream.Current;
        }
    }
    private static HttpRequestException Sanitized(Exception? error) => new("Assistant provider request failed.", null, error switch
    {
        AnthropicUnauthorizedException => HttpStatusCode.Unauthorized,
        AnthropicForbiddenException => HttpStatusCode.Forbidden,
        AnthropicBadRequestException => HttpStatusCode.BadRequest,
        AnthropicRateLimitException => HttpStatusCode.TooManyRequests,
        HttpRequestException request => request.StatusCode,
        _ => HttpStatusCode.ServiceUnavailable,
    });
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
