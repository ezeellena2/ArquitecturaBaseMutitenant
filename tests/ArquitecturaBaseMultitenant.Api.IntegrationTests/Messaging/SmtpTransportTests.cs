using System.Net;
using System.Net.Sockets;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

public sealed class SmtpTransportTests
{
    [Fact]
    public async Task Two_messages_in_one_batch_use_one_smtp_connection()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ct = TestContext.Current.CancellationToken;
        var accepting = ReceiveTwoMessagesAsync(listener, ct);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var transport = new SmtpEmailTransport(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = port,
            Security = SecureSocketOptions.None,
            UserName = "sender@example.test",
            Password = "test-password",
            FromAddress = "sender@example.test",
            FromName = "Test",
        }));

        try
        {
            await transport.SendAsync(new EmailMessage("one@example.test", "One", "<b>One</b>", "One"), ct);
            await transport.SendAsync(new EmailMessage("two@example.test", "Two", "<b>Two</b>", "Two"), ct);
            Assert.Equal(1, await accepting.WaitAsync(TimeSpan.FromSeconds(10), ct));
        }
        finally
        {
            if ((object)transport is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static async Task<int> ReceiveTwoMessagesAsync(TcpListener listener, CancellationToken ct)
    {
        var connections = 0;
        var delivered = 0;
        while (delivered < 2)
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            connections++;
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n",
            };
            await writer.WriteLineAsync("220 smtp.example.test ESMTP");
            while (delivered < 2 && await reader.ReadLineAsync(ct) is { } line)
            {
                if (line.StartsWith("EHLO ", StringComparison.Ordinal))
                {
                    await writer.WriteAsync("250-smtp.example.test\r\n250 AUTH PLAIN\r\n");
                }
                else if (line.StartsWith("AUTH PLAIN", StringComparison.Ordinal))
                {
                    if (line.Length == "AUTH PLAIN".Length)
                    {
                        await writer.WriteLineAsync("334");
                        await reader.ReadLineAsync(ct);
                    }
                    await writer.WriteLineAsync("235 Authenticated");
                }
                else if (line.StartsWith("MAIL FROM:", StringComparison.Ordinal) ||
                    line.StartsWith("RCPT TO:", StringComparison.Ordinal) ||
                    line == "RSET")
                {
                    await writer.WriteLineAsync("250 OK");
                }
                else if (line == "DATA")
                {
                    await writer.WriteLineAsync("354 End with dot");
                    while (await reader.ReadLineAsync(ct) is { } dataLine && dataLine != ".")
                    {
                    }
                    delivered++;
                    await writer.WriteLineAsync("250 Queued");
                }
                else if (line == "QUIT")
                {
                    await writer.WriteLineAsync("221 Bye");
                    break;
                }
                else
                {
                    throw new InvalidOperationException("Unexpected SMTP command in test server.");
                }
            }
        }

        return connections;
    }
}
