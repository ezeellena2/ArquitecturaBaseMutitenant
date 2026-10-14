using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppTeamNoticeRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppTeamNoticeRepository
{
    private const string PlatformLock = "module:whatsapp:platform-team-notices";

    public Task LockAsync(bool platform, CancellationToken ct) => context.AcquireAdvisoryLocksAsync(
        [platform ? PlatformLock : AdvisoryLockKeys.For(tenant.RequiredTenantId, "whatsapp-team-notices", "all")], ct);

    public async Task<IReadOnlyList<WhatsAppTeamNoticeBase>> ListAsync(bool platform, CancellationToken ct)
    {
        context.RequireTransaction();
        if (platform) return await context.Set<PlatformWhatsAppTeamNotice>().OrderBy(value => value.Id).ToArrayAsync(ct);
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppTeamNotice>().Where(value => value.TenantId == tenantId).OrderBy(value => value.Id).ToArrayAsync(ct);
    }

    public void Add(WhatsAppTeamNoticeBase notice) { context.RequireTransaction(); context.Add(notice); }
    public void Remove(WhatsAppTeamNoticeBase notice) { context.RequireTransaction(); context.Remove(notice); }
}
