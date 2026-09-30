using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>Agrega el mensaje cifrado al ChangeTracker del mismo caso de uso; UnitOfWork lo guarda.</summary>
internal sealed class Outbox(
    ApplicationDbContext context,
    IPayloadProtector protector,
    TimeProvider timeProvider,
    IEnumerable<IChannelSender> senders) : IOutbox
{
    private readonly HashSet<string> _channels = senders.Select(sender => sender.Key)
        .ToHashSet(StringComparer.Ordinal);

    public void Enqueue(string channel, string payload, Guid? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        context.RequireTransaction();

        if (!_channels.Contains(channel))
        {
            throw new ArgumentException("The outbox channel is not registered.", nameof(channel));
        }

        var encryptedPayload = protector.Protect(payload);
        context.OutboxMessages.Add(OutboxMessage.Enqueue(
            channel, encryptedPayload, timeProvider.GetUtcNow().UtcDateTime, userId));
    }
}
