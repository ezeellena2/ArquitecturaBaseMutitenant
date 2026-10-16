using System.Collections.Concurrent;
using System.Net;
using System.Text;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// El transporte de Anthropic (E9): cada cliente del SDK recibe su propio HttpClient del transporte nombrado, sobre el
/// mismo handler reutilizado, y cada pedido lleva sólo su clave. El transporte no suma los reintentos de ServiceDefaults:
/// ni su handler está en la cadena ni un 503 llega más de una vez al proveedor.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantAnthropicTransportTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Clients_share_the_pooled_handler_without_crossing_their_keys()
    {
        var provider = new KeyRecorder(HttpStatusCode.OK);
        await using var host = Host(provider);
        using var scope = host.Services.CreateScope();
        var clients = scope.ServiceProvider.GetRequiredService<AnthropicChatClientBuilder>();
        using var first = clients.Build("isolated-first-key", "isolated-transport-model", AssistantEffort.Low, 64);
        using var second = clients.Build("isolated-second-key", "isolated-transport-model", AssistantEffort.Low, 64);

        await first.GetResponseAsync([new ChatMessage(ChatRole.User, "Uno")], cancellationToken: Ct);
        await second.GetResponseAsync([new ChatMessage(ChatRole.User, "Dos")], cancellationToken: Ct);
        await first.GetResponseAsync([new ChatMessage(ChatRole.User, "Tres")], cancellationToken: Ct);

        Assert.Equal(["isolated-first-key", "isolated-second-key", "isolated-first-key"], provider.Keys);
    }

    [Fact]
    public async Task A_provider_failure_reaches_the_provider_once_without_global_retries()
    {
        var provider = new KeyRecorder(HttpStatusCode.ServiceUnavailable);
        await using var host = Host(provider);
        using var scope = host.Services.CreateScope();

        Assert.DoesNotContain(Chain(host.Services.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(AnthropicChatClientBuilder.TransportName)), handler => handler is ResilienceHandler);
        var result = await scope.ServiceProvider.GetRequiredService<AnthropicChatClientBuilder>()
            .ValidateDraftAsync("isolated-retry-key", "isolated-transport-model", Ct);

        Assert.Equal(AssistantErrors.ProviderUnavailableCode, result.Error.Code);
        Assert.Single(provider.Keys);
    }

    private WebApplicationFactory<Program> Host(KeyRecorder provider) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient(AnthropicChatClientBuilder.TransportName).ConfigurePrimaryHttpMessageHandler(() => provider)));

    private static IEnumerable<HttpMessageHandler> Chain(HttpMessageHandler handler)
    {
        for (HttpMessageHandler? current = handler; current is not null; current = (current as DelegatingHandler)?.InnerHandler)
            yield return current;
    }

    /// <summary>El proveedor: anota la clave de cada pedido que le llega. El factory lo comparte entre clientes.</summary>
    private sealed class KeyRecorder(HttpStatusCode status) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<string> _keys = new();

        internal IReadOnlyList<string> Keys => [.. _keys];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _keys.Enqueue(string.Join(',', request.Headers.TryGetValues("x-api-key", out var keys) ? keys : []));
            var payload = status == HttpStatusCode.OK ? """
                {"id":"isolated-message","type":"message","role":"assistant","model":"isolated-transport-model","content":[{"type":"text","text":"OK"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":1,"output_tokens":1}}
                """ : """
                {"type":"error","error":{"type":"overloaded_error","message":"isolated-provider-down"}}
                """;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
        }
    }
}
