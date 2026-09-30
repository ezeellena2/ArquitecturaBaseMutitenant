using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

internal sealed class SmtpEmailTransport(IOptions<SmtpOptions> options) : IEmailTransport, IDisposable
{
    private const int TimeoutMilliseconds = 30_000;
    private SmtpClient? _client;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var mime = MimeMessageFactory.Create(message, settings);
        var client = _client ??= new SmtpClient { Timeout = TimeoutMilliseconds };
        try
        {
            if (!client.IsConnected)
            {
                await client.ConnectAsync(settings.Host, settings.Port, settings.Security, cancellationToken);
                await client.AuthenticateAsync(settings.UserName, settings.Password, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
        }
        catch
        {
            // Una conexión fallida no se comparte con el siguiente mensaje del lote.
            client.Dispose();
            _client = null;
            throw;
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }
}
