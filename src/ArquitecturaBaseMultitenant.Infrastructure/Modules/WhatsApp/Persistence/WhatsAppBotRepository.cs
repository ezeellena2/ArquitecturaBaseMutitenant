using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppBotRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppBotRepository
{
    public Task LockAsync(Guid channelId, CancellationToken ct) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.For(tenant.RequiredTenantId, "whatsapp-bot", channelId.ToString("N"))], ct);

    public async Task<WhatsAppBot?> GetAsync(Guid channelId, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        var row = await context.Set<WhatsAppBot>().SingleOrDefaultAsync(value => value.TenantId == tenantId && value.ChannelId == channelId, ct);
        if (row is null || expectedVersion is not { } version) return row;
        if (row.Version != version) throw new ConcurrencyConflictException();
        context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }

    public void Add(WhatsAppBot bot) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Set<WhatsAppBot>().Add(bot); }
}
