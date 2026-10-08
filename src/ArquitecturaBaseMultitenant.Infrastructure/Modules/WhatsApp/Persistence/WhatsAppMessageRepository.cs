using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
internal sealed class WhatsAppMessageRepository(ApplicationDbContext context) : IWhatsAppMessageRepository
{
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
        return await context.Set<WhatsAppMessage>().Where(value => value.ProcessedAtUtc == null
            && (value.LeaseUntilUtc == null || value.LeaseUntilUtc <= nowUtc))
            .OrderBy(value => value.ReceivedAtUtc).ThenBy(value => value.Id).Take(limit).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<WhatsAppMessage>> ListExpiredTextAsync(DateTime beforeUtc, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<WhatsAppMessage>().Where(value => value.EncryptedText != null && value.ReceivedAtUtc < beforeUtc)
            .OrderBy(value => value.ReceivedAtUtc).ThenBy(value => value.Id).Take(limit).ToArrayAsync(ct);
    }
    public void Add(WhatsAppMessage message) { context.RequireTransaction(); context.Set<WhatsAppMessage>().Add(message); }
}
