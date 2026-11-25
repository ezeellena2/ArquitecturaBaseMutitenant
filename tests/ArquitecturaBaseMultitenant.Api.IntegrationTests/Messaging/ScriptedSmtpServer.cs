using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// N5 y N8, copiado de ArquitecturaBase: un servidor SMTP en el loopback que contesta una conexión con un guion, para probar
/// la clasificación del transporte contra respuestas reales del protocolo: el saludo, AUTH, MAIL FROM, RCPT TO y lo que hace
/// después del cuerpo (contestar o cortar). Un saludo nulo no saluda nunca, y <c>holdData</c> no contesta a DATA: los dos
/// esperan a que venza la espera del que envía.
/// </summary>
internal sealed class ScriptedSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;

    public ScriptedSmtpServer(
        string? greeting = "220 localhost ESMTP",
        string auth = "235 2.7.0 Accepted",
        string recipient = "250 2.1.5 OK",
        string? afterBody = "250 2.0.0 OK queued",
        string sender = "250 2.1.0 OK",
        bool holdData = false)
    {
        _listener.Start();
        _serving = ServeAsync(greeting, auth, sender, recipient, afterBody, holdData, _stop.Token);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public bool ReceivedBody { get; private set; }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _serving.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException
            or TimeoutException or OperationCanceledException)
        {
        }
        _listener.Dispose();
        _stop.Dispose();
    }

    private async Task ServeAsync(string? greeting, string auth, string sender, string recipient, string? afterBody, bool holdData,
        CancellationToken ct)
    {
        using var client = await _listener.AcceptTcpClientAsync(ct);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

        if (greeting is null)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return;
        }
        await writer.WriteLineAsync(greeting);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var command = line.ToUpperInvariant();
            if (command.StartsWith("EHLO", StringComparison.Ordinal))
            {
                await writer.WriteLineAsync("250-localhost");
                await writer.WriteLineAsync("250 AUTH PLAIN");
            }
            else if (command.StartsWith("AUTH PLAIN", StringComparison.Ordinal))
            {
                if (command.Trim() == "AUTH PLAIN")
                {
                    await writer.WriteLineAsync("334 ");
                    await reader.ReadLineAsync(ct);
                }
                await writer.WriteLineAsync(auth);
            }
            else if (command.StartsWith("MAIL FROM", StringComparison.Ordinal))
            {
                await writer.WriteLineAsync(sender);
            }
            else if (command.StartsWith("RCPT TO", StringComparison.Ordinal))
            {
                await writer.WriteLineAsync(recipient);
            }
            else if (command == "DATA")
            {
                if (holdData)
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return;
                }
                await writer.WriteLineAsync("354 Go ahead");
                while (await reader.ReadLineAsync(ct) is { } data && data != ".")
                {
                }
                ReceivedBody = true;
                if (afterBody is null) return;
                await writer.WriteLineAsync(afterBody);
            }
            else if (command == "QUIT")
            {
                await writer.WriteLineAsync("221 2.0.0 Bye");
                return;
            }
            else
            {
                await writer.WriteLineAsync("250 2.0.0 OK");
            }
        }
    }
}
