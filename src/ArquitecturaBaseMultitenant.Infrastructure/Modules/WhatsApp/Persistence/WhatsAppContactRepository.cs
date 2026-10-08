using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
internal sealed class WhatsAppContactRepository(ApplicationDbContext context) : IWhatsAppContactRepository
{
    public Task LockAsync(string waId, CancellationToken ct) =>
        context.AcquireAdvisoryLocksAsync(["module:whatsapp:contact:" + waId], ct);
    public Task<WhatsAppContact?> GetAsync(string waId, CancellationToken ct)
    {
        context.RequireTransaction();
        return context.Set<WhatsAppContact>().SingleOrDefaultAsync(value => value.WaId == waId, ct);
    }
    public async Task<IReadOnlyList<WhatsAppContact>> ListByMethodAsync(Guid methodId, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<WhatsAppContact>().Where(value => value.MethodId == methodId).ToArrayAsync(ct);
    }
    public void Add(WhatsAppContact contact) { context.RequireTransaction(); context.Set<WhatsAppContact>().Add(contact); }
    public void Remove(WhatsAppContact contact) { context.RequireTransaction(); context.Set<WhatsAppContact>().Remove(contact); }
}
