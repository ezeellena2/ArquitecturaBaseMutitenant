using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>
/// Agrega el mensaje cifrado al ChangeTracker del mismo caso de uso; UnitOfWork lo guarda. Del destino que lo ordena guarda
/// solo su HMAC.
/// </summary>
internal sealed class Outbox(
    ApplicationDbContext context,
    IPayloadProtector protector,
    TimeProvider timeProvider,
    IEnumerable<IChannelSender> senders,
    OutboxOrderingKeyHasher orderingKeys) : IOutbox
{
    private readonly HashSet<string> _channels = senders.Select(sender => sender.Key)
        .ToHashSet(StringComparer.Ordinal);

    public Guid Enqueue(string channel, string payload, Guid? userId = null, OutboxEnqueueOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        context.RequireTransaction();

        if (!_channels.Contains(channel))
        {
            throw new ArgumentException("The outbox channel is not registered.", nameof(channel));
        }

        options ??= new OutboxEnqueueOptions();
        var orderingKey = options.Destination is null ? null : orderingKeys.Hash(channel, options.Destination);
        var encryptedPayload = protector.Protect(payload);
        var message = OutboxMessage.Enqueue(channel, encryptedPayload, timeProvider.GetUtcNow().UtcDateTime, userId,
            orderingKey, options.Priority, options.DiscardAfterUtc);
        context.OutboxMessages.Add(message);
        return message.Id;
    }
}
