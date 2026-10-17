using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
internal sealed class WhatsAppMessageRepository(ApplicationDbContext context) : IWhatsAppMessageRepository
{
    public Task LockStreamAsync(string phoneNumberId, string wamid, CancellationToken ct) =>
        context.AcquireAdvisoryLocksAsync(["module:whatsapp:stream:" + phoneNumberId + ":" + wamid], ct);
    public Task LockEventAsync(string phoneNumberId, string wamid, string eventType, CancellationToken ct) =>
        context.AcquireAdvisoryLocksAsync(["module:whatsapp:event:" + phoneNumberId + ":" + wamid + ":" + eventType], ct);
    public Task<WhatsAppMessage?> GetEventAsync(string phoneNumberId, string wamid, string eventType, CancellationToken ct)
    {
        context.RequireTransaction();
        return context.Set<WhatsAppMessage>().SingleOrDefaultAsync(value => value.PhoneNumberId == phoneNumberId
            && value.Wamid == wamid && value.EventType == eventType, ct);
    }
    public async Task<IReadOnlyList<WhatsAppMessage>> ListPendingAsync(DateTime nowUtc, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<WhatsAppMessage>().Where(value => value.Direction == WhatsAppMessageDirection.Inbound && value.EventType == "inbound"
            && value.ProcessedAtUtc == null && value.NextAttemptAtUtc != null && value.NextAttemptAtUtc <= nowUtc
            && (value.LeaseUntilUtc == null || value.LeaseUntilUtc <= nowUtc))
            .OrderBy(value => value.ReceivedAtUtc).ThenBy(value => value.Id).Take(limit).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<WhatsAppMessage>> ListExpiredTextAsync(DateTime beforeUtc, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        var rows = await context.Set<WhatsAppMessage>().FromSqlInterpolated($"""
            SELECT *, xmin FROM identity."WhatsAppMessages"
            WHERE "EncryptedText" IS NOT NULL AND "ReceivedAtUtc" <= {beforeUtc}
            ORDER BY "ReceivedAtUtc", "Id" LIMIT {limit} FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct);
        foreach (var row in rows) await context.Entry(row).ReloadAsync(ct);
        return rows;
    }
    public async Task<IReadOnlyList<WhatsAppMessage>> ListOriginalForUpdateAsync(Guid userId, Guid? methodId, CancellationToken ct)
    {
        context.RequireTransaction();
        var rows = methodId is { } method
            ? await context.Set<WhatsAppMessage>().FromSqlInterpolated($"""
                SELECT *, xmin FROM identity."WhatsAppMessages"
                WHERE "OriginalUserId" = {userId} AND "OriginalMethodId" = {method}
                ORDER BY "Id" FOR UPDATE
                """).ToArrayAsync(ct)
            : await context.Set<WhatsAppMessage>().FromSqlInterpolated($"""
                SELECT *, xmin FROM identity."WhatsAppMessages" WHERE "OriginalUserId" = {userId}
                ORDER BY "Id" FOR UPDATE
                """).ToArrayAsync(ct);
        foreach (var row in rows) await context.Entry(row).ReloadAsync(ct);
        return rows;
    }
    public void Add(WhatsAppMessage message) { context.RequireTransaction(); context.Set<WhatsAppMessage>().Add(message); }
    public Task<WhatsAppInboundHint?> GetProcessingHintAsync(Guid messageId, CancellationToken ct)
    {
        context.RequireTransaction();
        return (from message in context.Set<WhatsAppMessage>().AsNoTracking()
                join contact in context.Set<WhatsAppContact>().AsNoTracking() on message.ContactId equals contact.Id into contacts
                from contact in contacts.DefaultIfEmpty()
                where message.Id == messageId
                select new WhatsAppInboundHint(message.Id, contact == null ? null : contact.WaId, message.OriginalUserId, message.OriginalMethodId)).SingleOrDefaultAsync(ct);
    }
    public async Task<WhatsAppMessage?> GetForUpdateAsync(Guid messageId, CancellationToken ct)
    {
        context.RequireTransaction();
        var rows = await context.Set<WhatsAppMessage>().FromSqlInterpolated($"SELECT *, xmin FROM identity.\"WhatsAppMessages\" WHERE \"Id\" = {messageId} FOR UPDATE").ToArrayAsync(ct);
        var message = rows.SingleOrDefault();
        if (message is not null) await context.Entry(message).ReloadAsync(ct);
        return message;
    }
    public async Task<WhatsAppInboundClaim?> ClaimNextAsync(DateTime nowUtc, TimeSpan leaseLifetime, int maxAttempts, CancellationToken ct)
    {
        context.RequireTransaction();
        var exhausted = new List<Guid>();
        for (var index = 0; index < 50; index++)
        {
            var rows = await context.Set<WhatsAppMessage>().FromSqlInterpolated($"""
                SELECT *, xmin FROM identity."WhatsAppMessages"
                WHERE "Direction" = 'Inbound' AND "EventType" = 'inbound' AND "ProcessedAtUtc" IS NULL
                  AND "NextAttemptAtUtc" <= {nowUtc} AND ("LeaseUntilUtc" IS NULL OR "LeaseUntilUtc" <= {nowUtc})
                  AND NOT ("Id" = ANY({exhausted.ToArray()}))
                ORDER BY "NextAttemptAtUtc", "ReceivedAtUtc", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
                """).ToArrayAsync(ct);
            var message = rows.SingleOrDefault();
            if (message is null) return null;
            await context.Entry(message).ReloadAsync(ct);
            if (message.TryLease(Guid.CreateVersion7(), nowUtc, leaseLifetime, maxAttempts))
            {
                // La entrada de un número de organización se procesa dentro de su alcance (decisión 4 de la E11).
                var organizationId = message.ChannelId is { } channelId
                    ? await context.Set<WhatsAppChannel>().AsNoTracking().Where(value => value.Id == channelId).Select(value => value.OrganizationId).SingleOrDefaultAsync(ct)
                    : null;
                return new(message.Id, message.LeaseId!.Value, organizationId);
            }
            exhausted.Add(message.Id);
        }
        return null;
    }
    public Task<Guid?> GetOutboxUserIdAsync(Guid outboxMessageId, CancellationToken ct)
    {
        context.RequireTransaction();
        return context.OutboxMessages.Where(value => value.Id == outboxMessageId).Select(value => value.UserId).SingleOrDefaultAsync(ct);
    }
    public async Task<IReadOnlyList<WhatsAppMessage>> ListDeliveryEventsAsync(string phoneNumberId, string wamid, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<WhatsAppMessage>().AsNoTracking().Where(value => value.PhoneNumberId == phoneNumberId
            && value.Wamid == wamid && value.Kind == WhatsAppMessageKind.Delivery).ToArrayAsync(ct);
    }
}
