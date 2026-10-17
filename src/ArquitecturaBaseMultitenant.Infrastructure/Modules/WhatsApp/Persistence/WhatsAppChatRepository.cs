using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppChatRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppChatRepository
{
    public Task LockAsync(Guid channelId, string waId, CancellationToken ct) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.For(tenant.RequiredTenantId, "whatsapp-chat", channelId.ToString("N") + ":" + waId)], ct);

    public Task<WhatsAppChat?> GetAsync(Guid channelId, string waId, WhatsAppInbox inbox, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppChat>().SingleOrDefaultAsync(value => value.TenantId == tenantId && value.ChannelId == channelId
            && value.WaId == waId && value.Inbox == inbox, ct);
    }

    public async Task<WhatsAppChat?> GetForUpdateAsync(Guid id, WhatsAppInbox inbox, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        var rows = await context.Set<WhatsAppChat>().FromSqlInterpolated($"""
            SELECT *, xmin FROM tenant."WhatsAppChats" WHERE "TenantId" = {tenantId} AND "Id" = {id} AND "Inbox" = {inbox.ToString()} FOR UPDATE
            """).ToArrayAsync(ct);
        var chat = rows.SingleOrDefault();
        if (chat is null) return null;
        await context.Entry(chat).ReloadAsync(ct);
        if (expectedVersion is { } version)
        {
            if (chat.Version != version) throw new ConcurrencyConflictException();
            context.Entry(chat).Property(value => value.Version).OriginalValue = version;
        }
        return chat;
    }

    public Task<bool> HasInboundAsync(string wamid, CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppChatMessage>().AnyAsync(value => value.TenantId == tenantId && value.Wamid == wamid, ct);
    }

    public Task<WhatsAppChatMessage?> GetOutboundAsync(Guid outboxMessageId, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppChatMessage>().SingleOrDefaultAsync(value => value.TenantId == tenantId && value.OutboxMessageId == outboxMessageId, ct);
    }

    public async Task<IReadOnlyList<WhatsAppChat>> ListTakenByAsync(Guid userId, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppChat>().Where(value => value.TenantId == tenantId && value.AssigneeUserId == userId).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<WhatsAppChatMessage>> ListAuthoredByAsync(Guid userId, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppChatMessage>().Where(value => value.TenantId == tenantId && value.AuthorUserId == userId).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<WhatsAppChat>> ListByTeamAsync(Guid teamId, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppChat>().Where(value => value.TenantId == tenantId && value.TeamId == teamId).ToArrayAsync(ct);
    }

    public void Add(WhatsAppChat chat) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Set<WhatsAppChat>().Add(chat); }
    public void Add(WhatsAppChatMessage message) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Set<WhatsAppChatMessage>().Add(message); }
}
