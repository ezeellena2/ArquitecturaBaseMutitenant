using System.Net;
using System.Net.Sockets;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// Revisión adversarial de la B5: lo que no llegó a salir se reintenta, aunque MailKit lo diga con NotSupportedException antes
/// de aceptar al destinatario, y la espera y el tope de un canal no se ignoran por la forma en que se escribe su clave.
/// </summary>
public sealed class OutboxB5AdversarialTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Con <c>Security = StartTls</c> y un servidor que no ofrece STARTTLS (mal configurado, o un intermediario que lo saca),
    /// MailKit corta en ConnectAsync: no salió nada. Hoy cae en la rama de SMTPUTF8 y el correo falla para siempre al primer
    /// intento; al corregir la configuración ya no se manda.
    /// </summary>
    [Fact]
    public async Task A_server_that_does_not_offer_starttls_sent_nothing_and_the_mail_is_retried()
    {
        await using var server = new ScriptedSmtpServer();
        using var transport = Transport(server.Port, SecureSocketOptions.StartTls);

        var outcome = await transport.SendAsync(Message(), Ct);

        Assert.False(server.ReceivedBody);
        Assert.Equal(EmailSendOutcome.NotSent, outcome);
    }

    /// <summary>
    /// Un servidor que no anuncia AUTH (otro puerto, un relay, o STARTTLS que no se hizo): MailKit lanza NotSupportedException
    /// en AuthenticateAsync, antes del mensaje. No salió nada y debe volver a pendiente, no fallar para siempre.
    /// </summary>
    [Fact]
    public async Task A_server_that_does_not_offer_auth_sent_nothing_and_the_mail_is_retried()
    {
        await using var server = new NoAuthSmtpServer();
        using var transport = Transport(server.Port, SecureSocketOptions.None);

        var outcome = await transport.SendAsync(Message(), Ct);

        Assert.False(server.ReceivedBody);
        Assert.Equal(EmailSendOutcome.NotSent, outcome);
    }

    /// <summary>
    /// Las claves de configuración no distinguen mayúsculas, y todas las secciones del proyecto van en PascalCase
    /// (<c>Email__Smtp__Password</c>). <c>Messaging__Outbox__Channels__Email__MaxAttempts</c> se ignora en silencio porque
    /// <c>Channels</c> compara la clave del canal con <see cref="StringComparer.Ordinal"/>, y el validador no lo avisa.
    /// </summary>
    [Fact]
    public void A_channel_key_written_like_every_other_section_is_not_ignored()
    {
        var options = new OutboxOptions();
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Messaging:Outbox:Channels:Email:MaxAttempts"] = "1",
            ["Messaging:Outbox:Channels:Email:DeliveryTimeoutSeconds"] = "8",
        }).Build().GetSection(OutboxOptions.SectionName).Bind(options);

        Assert.Equal(1, options.MaxAttemptsFor(OutboxChannel.Email));
        Assert.Equal(TimeSpan.FromSeconds(8), options.DeliveryTimeoutFor(OutboxChannel.Email));
    }

    private static EmailMessage Message() => new("recipient@example.test", "Subject", "<b>Body</b>", "Body");

    private static SmtpEmailTransport Transport(int port, SecureSocketOptions security) => new(Options.Create(new SmtpOptions
    {
        Host = "127.0.0.1",
        Port = port,
        Security = security,
        UserName = "sender@example.test",
        Password = "test-password",
        FromAddress = "sender@example.test",
        FromName = "Test",
    }), new EmailHealth(), NullLogger<SmtpEmailTransport>.Instance);

    /// <summary>Un servidor SMTP de una conexión que no anuncia AUTH; acepta todo lo demás.</summary>
    private sealed class NoAuthSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serving;

        public NoAuthSmtpServer()
        {
            _listener.Start();
            _serving = ServeAsync(_stop.Token);
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

        private async Task ServeAsync(CancellationToken ct)
        {
            using var client = await _listener.AcceptTcpClientAsync(ct);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 localhost ESMTP");
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                var command = line.ToUpperInvariant();
                if (command.StartsWith("EHLO", StringComparison.Ordinal))
                {
                    await writer.WriteLineAsync("250 localhost");
                }
                else if (command == "DATA")
                {
                    await writer.WriteLineAsync("354 Go ahead");
                    while (await reader.ReadLineAsync(ct) is { } data && data != ".")
                    {
                    }
                    ReceivedBody = true;
                    await writer.WriteLineAsync("250 2.0.0 OK queued");
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
}
