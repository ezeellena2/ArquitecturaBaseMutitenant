using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

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
    OutboxOrderingKeyHasher orderingKeys,
    ITenantContext? tenantContext = null) : IOutbox
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
        var tenantId = options.TenantId ?? (tenantContext?.TenantId is { } active && active != Guid.Empty ? active : null);
        var message = OutboxMessage.Enqueue(channel, encryptedPayload, timeProvider.GetUtcNow().UtcDateTime, userId,
            orderingKey, options.Priority, options.DiscardAfterUtc, tenantId);
        context.OutboxMessages.Add(message);
        return message.Id;
    }

    /// <summary>
    /// <c>SKIP LOCKED</c> no espera a la copia que lo está reservando: esa reserva sigue, y el canal decide si sale. Bajo el lock
    /// se relee la fila, porque el DbContext puede tener una versión anterior.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> WithdrawAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        context.RequireTransaction();
        var withdrawn = new HashSet<Guid>();
        if (messageIds.Count == 0) return withdrawn;
        var ids = messageIds.ToArray();
        var messages = await context.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM platform."OutboxMessages"
            WHERE "Id" = ANY({ids}) AND "Status" IN ('Pending', 'Failed') AND "NextAttemptAtUtc" IS NOT NULL AND "LeaseId" IS NULL
            ORDER BY "Id" FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct);
        foreach (var message in messages)
        {
            await context.Entry(message).ReloadAsync(ct);
            if (message.TryWithdraw()) withdrawn.Add(message.Id);
        }
        return withdrawn;
    }
}
