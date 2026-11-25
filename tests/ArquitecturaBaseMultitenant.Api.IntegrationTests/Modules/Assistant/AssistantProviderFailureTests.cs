using System.Net;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.Extensions.AI;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// ADR 0064 §7 (B13): cada falla del proveedor con su código, por el estado HTTP o por su transporte, a través del SDK real de
/// Anthropic y de <see cref="AssistantChatClient"/>, sin leer el cuerpo y sin repetir el pedido. Sin Docker ni proveedor real.
/// </summary>
public sealed class AssistantProviderFailureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Body = "isolated-sensitive-provider-payload";

    public static TheoryData<int, string> Statuses() => new()
    {
        { 400, AssistantErrors.ProviderModelRejectedCode },
        { 401, AssistantErrors.ProviderCredentialRejectedCode },
        { 402, AssistantErrors.ProviderLimitedCode },
        { 403, AssistantErrors.ProviderCredentialRejectedCode },
        { 404, AssistantErrors.ProviderModelRejectedCode },
        { 408, AssistantErrors.ProviderRejectedCode },
        { 409, AssistantErrors.ProviderRejectedCode },
        { 413, AssistantErrors.ProviderRejectedCode },
        { 422, AssistantErrors.ProviderRejectedCode },
        { 429, AssistantErrors.ProviderLimitedCode },
        { 500, AssistantErrors.ProviderUncertainCode },
        { 503, AssistantErrors.ProviderUncertainCode },
        { 529, AssistantErrors.ProviderUncertainCode },
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task Each_provider_status_has_its_code_without_the_body_or_a_second_request(int status, string code)
    {
        foreach (var streaming in new[] { false, true })
        {
            var handler = new Provider((_, _) => Task.FromResult(Response(status)));
            var failure = await FailureAsync(new HttpClient(handler), streaming);
            Assert.Equal(code, AssistantProviderFailureClassifier.Classify(failure));
            Assert.Equal((HttpStatusCode)status, failure.StatusCode);
            Assert.Null(failure.InnerException);
            Assert.DoesNotContain(Body, failure.ToString(), StringComparison.Ordinal);
            Assert.Equal(1, handler.Requests);
        }
    }

    public static TheoryData<HttpRequestError> NothingSent() =>
        [HttpRequestError.NameResolutionError, HttpRequestError.ConnectionError, HttpRequestError.SecureConnectionError];

    [Theory]
    [MemberData(nameof(NothingSent))]
    public async Task Without_a_connection_nothing_was_sent_and_the_provider_is_unavailable(HttpRequestError error)
    {
        foreach (var streaming in new[] { false, true })
        {
            var failure = await FailureAsync(new HttpClient(new Provider((_, _) => throw new HttpRequestException(error, Body))), streaming);
            Assert.Equal(AssistantErrors.ProviderUnavailableCode, AssistantProviderFailureClassifier.Classify(failure));
            Assert.DoesNotContain(Body, failure.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_ten_seconds_to_connect_are_unavailable_and_the_wait_after_sending_is_uncertain()
    {
        // The sockets handler reports its connect timeout as a cancellation with a TimeoutException inside, while the request is alive.
        var connect = new AssistantProviderConnectHandler
        {
            InnerHandler = new Provider((_, _) => throw new TaskCanceledException("connect", new TimeoutException("connect"))),
        };
        Assert.Equal(TimeSpan.FromSeconds(10), AssistantProviderConnectHandler.ConnectTimeout);
        Assert.Equal(AssistantErrors.ProviderUnavailableCode, AssistantProviderFailureClassifier.Classify(await FailureAsync(new HttpClient(connect), true)));
        // The SDK's own wait cancels the request: it was sent and may have started, so it is uncertain.
        var slow = new AssistantProviderConnectHandler
        {
            InnerHandler = new Provider(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Response(200); }),
        };
        foreach (var streaming in new[] { false, true })
        {
            var failure = await FailureAsync(new HttpClient(slow, false), streaming, TimeSpan.FromMilliseconds(200));
            Assert.Equal(AssistantErrors.ProviderUncertainCode, AssistantProviderFailureClassifier.Classify(failure));
        }
    }

    [Fact]
    public async Task A_cut_response_is_uncertain_and_the_caller_cancellation_is_not_a_provider_failure()
    {
        var cut = await FailureAsync(new HttpClient(new Provider((_, _) => throw new IOException(Body))), true);
        Assert.Equal(AssistantErrors.ProviderUncertainCode, AssistantProviderFailureClassifier.Classify(cut));
        Assert.DoesNotContain(Body, cut.ToString(), StringComparison.Ordinal);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        using var client = AnthropicChatClientBuilder.Build("isolated-failure-key", "isolated-failure-model", AssistantEffort.Low, 64,
            new HttpClient(new Provider((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hola")], cancellationToken: canceled.Token));
    }

    private static async Task<HttpRequestException> FailureAsync(HttpClient transport, bool streaming, TimeSpan? timeout = null)
    {
        using var client = AnthropicChatClientBuilder.Build("isolated-failure-key", "isolated-failure-model", AssistantEffort.Low, 64, transport, timeout);
        return await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            if (!streaming) await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hola")], cancellationToken: Ct);
            else await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hola")], cancellationToken: Ct)) { }
        });
    }

    private static HttpResponseMessage Response(int status) => new((HttpStatusCode)status)
    {
        Content = new StringContent(status == 200 ? "{}" : $$$"""{"type":"error","error":{"type":"error","message":"{{{Body}}}"}}""", Encoding.UTF8, "application/json"),
    };

    private sealed class Provider(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        private int requests;
        internal int Requests => Volatile.Read(ref requests);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requests);
            return send(request, cancellationToken);
        }
    }
}
