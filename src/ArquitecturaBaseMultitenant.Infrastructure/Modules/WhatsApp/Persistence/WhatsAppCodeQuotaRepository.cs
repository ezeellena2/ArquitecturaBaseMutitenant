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
    public async Task<WhatsAppCodeQuotaIp?> GetIpAsync(Guid quotaId, string ipKey, CancellationToken ct)
    {
        context.RequireTransaction();
        var local = context.Set<WhatsAppCodeQuotaIp>().Local.SingleOrDefault(value => value.QuotaId == quotaId && value.IpKey == ipKey);
        if (local is not null)
        {
            if (context.Entry(local).State == EntityState.Unchanged) await context.Entry(local).ReloadAsync(ct);
            return local;
        }
        return await context.Set<WhatsAppCodeQuotaIp>().SingleOrDefaultAsync(value => value.QuotaId == quotaId && value.IpKey == ipKey, ct);
    }
    public void Add(WhatsAppCodeQuotaIp quota) { context.RequireTransaction(); context.Set<WhatsAppCodeQuotaIp>().Add(quota); }
    public async Task<IReadOnlyList<WhatsAppCodeQuotaIp>> ListExpiredIpsAsync(DateOnly todayUtc, int count, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<WhatsAppCodeQuotaIp>().FromSqlInterpolated($"""
            SELECT ip.* FROM identity."WhatsAppCodeQuotaIps" ip
            JOIN identity."WhatsAppCodeQuotas" q ON q."Id" = ip."QuotaId"
            WHERE q."DayUtc" < {todayUtc} ORDER BY q."DayUtc", ip."Id"
            LIMIT {count} FOR UPDATE OF ip SKIP LOCKED
            """).ToArrayAsync(ct);
    }
    public void Remove(WhatsAppCodeQuotaIp quota) { context.RequireTransaction(); context.Set<WhatsAppCodeQuotaIp>().Remove(quota); }
}
