using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
internal sealed class WhatsAppCodeQuotaRepository(ApplicationDbContext context) : IWhatsAppCodeQuotaRepository
{
    public Task LockAsync(string phoneNumberId, DateOnly dayUtc, CancellationToken ct) => context.AcquireAdvisoryLocksAsync(
        ["module:whatsapp:quota:" + phoneNumberId + ":" + JsonSerializer.Serialize(dayUtc.DayNumber)], ct);
    public async Task<WhatsAppCodeQuota?> GetAsync(string phoneNumberId, DateOnly dayUtc, CancellationToken ct)
    {
        context.RequireTransaction();
        var local = context.Set<WhatsAppCodeQuota>().Local.SingleOrDefault(value => value.PhoneNumberId == phoneNumberId && value.DayUtc == dayUtc);
        if (local is not null)
        {
            if (context.Entry(local).State == EntityState.Unchanged) await context.Entry(local).ReloadAsync(ct);
            return local;
        }
        return await context.Set<WhatsAppCodeQuota>().SingleOrDefaultAsync(value => value.PhoneNumberId == phoneNumberId && value.DayUtc == dayUtc, ct);
    }
    public void Add(WhatsAppCodeQuota quota) { context.RequireTransaction(); context.Set<WhatsAppCodeQuota>().Add(quota); }
}
