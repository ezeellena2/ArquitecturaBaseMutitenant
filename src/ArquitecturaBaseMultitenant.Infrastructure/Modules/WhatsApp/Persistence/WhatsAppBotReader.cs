using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppBotReader(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppBotReader
{
    public Task<WhatsAppBot?> FindAsync(Guid channelId, CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppBot>().AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == tenantId && value.ChannelId == channelId, ct);
    }

    public Task<PlatformWhatsAppBot?> FindPlatformAsync(CancellationToken ct) =>
        context.Set<PlatformWhatsAppBot>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == PlatformWhatsAppBot.SingletonId, ct);
}
