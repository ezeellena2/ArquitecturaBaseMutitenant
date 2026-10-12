using System.Net;
using System.Text;
using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;

/// <summary>Lee frames en cuanto llegan; no carga el cuerpo completo ni libera el proveedor para poder leer.</summary>
internal sealed class AssistantSseTestReader(StreamReader reader) : IDisposable
{
    internal sealed record Frame(string Event, string? Id, JsonElement Data);
    internal static async Task<AssistantSseTestReader> OpenAsync(HttpResponseMessage response, CancellationToken ct)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        return new(new StreamReader(await response.Content.ReadAsStreamAsync(ct), Encoding.UTF8));
    }

    internal async Task<Frame> NextAsync(CancellationToken ct)
    {
        string? name = null;
        string? id = null;
        var data = new List<string>();
        using var frameTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        frameTimeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            // Sólo un plazo de protección ante un stream colgado; el test no compara tiempos reales.
            var line = await reader.ReadLineAsync(frameTimeout.Token);
            Assert.NotNull(line);
            if (line.Length == 0)
            {
                if (data.Count == 0) continue;
                Assert.NotNull(name);
                using var payload = JsonDocument.Parse(string.Join('\n', data));
                Assert.False(payload.RootElement.TryGetProperty("event", out _));
                return new(name, id, payload.RootElement.Clone());
            }
            if (line.StartsWith(':')) continue;
            var separator = line.IndexOf(':');
            if (separator < 0) continue;
            var value = line[(separator + 1)..];
            if (value.StartsWith(' ')) value = value[1..];
            switch (line[..separator])
            {
                case "event": name = value; break;
                case "id": id = value; break;
                case "data": data.Add(value); break;
            }
        }
    }

    internal async Task<Frame> TerminalAsync(CancellationToken ct)
    {
        while (true)
        {
            var frame = await NextAsync(ct);
            if (frame.Event is "done" or "error") return frame;
        }
    }

    public void Dispose() => reader.Dispose();
}
