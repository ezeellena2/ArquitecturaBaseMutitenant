using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

internal sealed class EmailChannelSender(IEmailTransport transport) : IChannelSender
{
    public string Key => OutboxChannel.Email;

    public Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<EmailMessage>(payload)
            ?? throw new InvalidOperationException("The email outbox payload is empty.");
        return transport.SendAsync(message, cancellationToken);
    }
}
