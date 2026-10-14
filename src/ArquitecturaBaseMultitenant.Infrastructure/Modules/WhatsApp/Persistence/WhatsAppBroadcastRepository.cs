using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppBroadcastRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppBroadcastRepository
{
    public async Task<WhatsAppBroadcast?> GetForUpdateAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        var rows = await context.Set<WhatsAppBroadcast>().FromSqlInterpolated($"""
            SELECT *, xmin FROM tenant."WhatsAppBroadcasts" WHERE "TenantId" = {tenantId} AND "Id" = {id} FOR UPDATE
            """).ToArrayAsync(ct);
        var broadcast = rows.SingleOrDefault();
        if (broadcast is null) return null;
        await context.Entry(broadcast).ReloadAsync(ct);
        if (expectedVersion is { } version)
        {
            if (broadcast.Version != version) throw new ConcurrencyConflictException();
            context.Entry(broadcast).Property(value => value.Version).OriginalValue = version;
        }
        return broadcast;
    }

    public async Task<IReadOnlyList<WhatsAppBroadcastRecipient>> ListPendingAsync(Guid broadcastId, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppBroadcastRecipient>()
            .Where(value => value.TenantId == tenantId && value.BroadcastId == broadcastId && value.Status == WhatsAppRecipientStatus.Pending)
            .OrderBy(value => value.Id).Take(limit).ToArrayAsync(ct);
    }

    public async Task<WhatsAppBroadcastRecipient?> FindLatestSentAsync(Guid customerId, DateTime sinceUtc, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppBroadcastRecipient>()
            .Where(value => value.TenantId == tenantId && value.CustomerId == customerId
                && (value.Status == WhatsAppRecipientStatus.Sent || value.Status == WhatsAppRecipientStatus.Replied)
                && value.SentAtUtc >= sinceUtc)
            .OrderByDescending(value => value.SentAtUtc).ThenByDescending(value => value.Id).FirstOrDefaultAsync(ct);
    }

    public void Add(WhatsAppBroadcast broadcast) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Set<WhatsAppBroadcast>().Add(broadcast); }

    public void AddRecipients(IEnumerable<WhatsAppBroadcastRecipient> recipients)
    {
        context.RequireTransaction();
        _ = tenant.RequiredTenantId;
        context.Set<WhatsAppBroadcastRecipient>().AddRange(recipients);
    }
}
