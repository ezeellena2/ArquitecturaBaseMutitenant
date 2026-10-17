using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class PlatformWhatsAppChatRepository(ApplicationDbContext context) : IPlatformWhatsAppChatRepository
{
    public Task LockAsync(Guid channelId, string waId, CancellationToken ct) =>
        context.AcquireAdvisoryLocksAsync(["module:whatsapp:platform-chat:" + channelId.ToString("N") + ":" + waId], ct);

    public Task<PlatformWhatsAppChat?> GetAsync(Guid channelId, string waId, CancellationToken ct)
    {
        context.RequireTransaction();
        return context.Set<PlatformWhatsAppChat>().SingleOrDefaultAsync(value => value.ChannelId == channelId && value.WaId == waId, ct);
    }

    public async Task<PlatformWhatsAppChat?> GetForUpdateAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var rows = await context.Set<PlatformWhatsAppChat>().FromSqlInterpolated($"""
            SELECT *, xmin FROM platform."PlatformWhatsAppChats" WHERE "Id" = {id} FOR UPDATE
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

    public Task<bool> HasInboundAsync(string wamid, CancellationToken ct) =>
        context.Set<PlatformWhatsAppChatMessage>().AnyAsync(value => value.Wamid == wamid, ct);

    public Task<PlatformWhatsAppChatMessage?> GetOutboundAsync(Guid outboxMessageId, CancellationToken ct)
    {
        context.RequireTransaction();
        return context.Set<PlatformWhatsAppChatMessage>().SingleOrDefaultAsync(value => value.OutboxMessageId == outboxMessageId, ct);
    }

    public async Task<IReadOnlyList<PlatformWhatsAppChat>> ListTakenByAsync(Guid userId, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<PlatformWhatsAppChat>().Where(value => value.AssigneeUserId == userId).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<PlatformWhatsAppChatMessage>> ListAuthoredByAsync(Guid userId, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<PlatformWhatsAppChatMessage>().Where(value => value.AuthorUserId == userId).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<PlatformWhatsAppChat>> ListByTeamAsync(Guid teamId, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<PlatformWhatsAppChat>().Where(value => value.TeamId == teamId).ToArrayAsync(ct);
    }

    public void Add(PlatformWhatsAppChat chat) { context.RequireTransaction(); context.Set<PlatformWhatsAppChat>().Add(chat); }
    public void Add(PlatformWhatsAppChatMessage message) { context.RequireTransaction(); context.Set<PlatformWhatsAppChatMessage>().Add(message); }
}
