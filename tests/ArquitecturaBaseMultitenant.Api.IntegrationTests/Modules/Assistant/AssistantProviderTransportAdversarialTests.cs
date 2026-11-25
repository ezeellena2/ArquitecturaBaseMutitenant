using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Revisión adversarial de la B13: el transporte instalado de Anthropic, con un proveedor local de prueba (sin red ni proveedor real).
/// <see cref="AssistantProviderFailureClassifier"/> lee una redirección como un rechazo «que no se sigue», y un pedido que cuesta
/// no se repite. Si el handler primario siguiera las redirecciones, un 307 haría que el mismo POST saliera otra vez, y con el
/// encabezado <c>x-api-key</c>, a otro host: por eso no las sigue.
/// </summary>
public sealed class AssistantProviderTransportAdversarialTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Key = "isolated-redirect-key";

    [Fact]
    public async Task A_provider_redirect_is_rejected_without_sending_the_paid_request_again_nor_the_key_to_another_host()
    {
        using var provider = new RedirectingProvider();
        await using var services = new ServiceCollection().AddLogging().AddAssistantModule().BuildServiceProvider();
        var installed = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(AnthropicChatClientBuilder.TransportName);
        using var transport = new HttpClient(new ToLocalProvider(provider.Port) { InnerHandler = installed });
        using var client = AnthropicChatClientBuilder.Build(Key, "isolated-redirect-model", AssistantEffort.Low, 64, transport, TimeSpan.FromSeconds(20));

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hola")], cancellationToken: Ct));

        // One call, one request: the redirect target never receives the POST nor the key.
        Assert.Equal(["127.0.0.1 /v1/messages " + Key], provider.Requests);
        Assert.Equal(AssistantErrors.ProviderRejectedCode, AssistantProviderFailureClassifier.Classify(Assert.IsType<HttpRequestException>(failure)));
    }

    /// <summary>Lleva el pedido del SDK al proveedor local, sin tocar la cadena instalada que va debajo.</summary>
    private sealed class ToLocalProvider(int port) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri = new UriBuilder(request.RequestUri!) { Scheme = "http", Host = "127.0.0.1", Port = port }.Uri;
            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Un servidor HTTP/1.1 mínimo en el loopback: al primer pedido responde 307 hacia otro host (<c>localhost</c>), y ahí 503.
    /// Anota el host, la ruta y la clave de cada pedido que le llega.
    /// </summary>
    private sealed class RedirectingProvider : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.IPv6Any, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentQueue<string> _requests = new();

        internal RedirectingProvider()
        {
            _listener.Server.DualMode = true;
            _listener.Start();
            _ = Task.Run(AcceptAsync);
        }

        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        internal IReadOnlyList<string> Requests => [.. _requests];

        private async Task AcceptAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient connection;
                try { connection = await _listener.AcceptTcpClientAsync(_stop.Token); }
                catch (Exception) { return; }
                _ = Task.Run(() => ServeAsync(connection));
            }
        }

        private async Task ServeAsync(TcpClient connection)
        {
            using (connection)
            {
                var stream = connection.GetStream();
                var buffer = new List<byte>();
                var chunk = new byte[8192];
                int end;
                while ((end = IndexOf(buffer, "\r\n\r\n"u8.ToArray())) < 0)
                {
                    var read = await stream.ReadAsync(chunk, _stop.Token);
                    if (read == 0) return;
                    buffer.AddRange(chunk.AsSpan(0, read).ToArray());
                }
                var head = Encoding.ASCII.GetString(buffer.GetRange(0, end).ToArray()).Split("\r\n");
                var headers = head.Skip(1).Select(line => line.Split(':', 2)).Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
                var body = buffer.Skip(end + 4).ToList();
                if (headers.TryGetValue("Content-Length", out var length))
                    while (body.Count < int.Parse(length, System.Globalization.CultureInfo.InvariantCulture))
                    {
                        var read = await stream.ReadAsync(chunk, _stop.Token);
                        if (read == 0) break;
                        body.AddRange(chunk.AsSpan(0, read).ToArray());
                    }
                else if (headers.TryGetValue("Transfer-Encoding", out var encoding) && encoding.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                    while (IndexOf(body, "0\r\n\r\n"u8.ToArray()) < 0)
                    {
                        var read = await stream.ReadAsync(chunk, _stop.Token);
                        if (read == 0) break;
                        body.AddRange(chunk.AsSpan(0, read).ToArray());
                    }
                var path = head[0].Split(' ')[1];
                var host = headers.TryGetValue("Host", out var value) ? value.Split(':')[0] : "";
                _requests.Enqueue($"{host} {path} {(headers.TryGetValue("x-api-key", out var key) ? key : "")}");
                var error = """{"type":"error","error":{"type":"api_error","message":"isolated"}}""";
                var response = path == "/v1/messages"
                    ? $"HTTP/1.1 307 Temporary Redirect\r\nLocation: http://localhost:{Port}/elsewhere/v1/messages\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    : $"HTTP/1.1 503 Service Unavailable\r\nContent-Type: application/json\r\nContent-Length: {error.Length}\r\nConnection: close\r\n\r\n{error}";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stop.Token);
            }
        }

        private static int IndexOf(List<byte> source, byte[] pattern)
        {
            for (var index = 0; index <= source.Count - pattern.Length; index++)
                if (pattern.Select((value, offset) => source[index + offset] == value).All(match => match)) return index;
            return -1;
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _stop.Dispose();
        }
    }
}
