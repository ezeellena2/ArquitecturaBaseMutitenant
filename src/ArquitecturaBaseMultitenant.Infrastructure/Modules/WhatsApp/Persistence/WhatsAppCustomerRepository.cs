using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppCustomerRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppCustomerRepository
{
    public Task LockAsync(CancellationToken ct) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.For(tenant.RequiredTenantId, "whatsapp-customers", "all")], ct);

    public async Task<WhatsAppCustomer?> GetByWaIdAsync(string waId, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        // Una importación puede agregar el mismo número dos veces antes de guardar.
        return context.Set<WhatsAppCustomer>().Local.SingleOrDefault(value => value.WaId == waId)
            ?? await context.Set<WhatsAppCustomer>().SingleOrDefaultAsync(value => value.TenantId == tenantId && value.WaId == waId, ct);
    }

    public async Task<IReadOnlyList<WhatsAppCustomer>> ListByWaIdsAsync(IReadOnlyCollection<string> waIds, CancellationToken ct)
    {
        context.RequireTransaction();
        if (waIds.Count == 0) return [];
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppCustomer>().Where(value => value.TenantId == tenantId && waIds.Contains(value.WaId)).ToArrayAsync(ct);
    }

    public async Task<WhatsAppCustomer?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        var row = await context.Set<WhatsAppCustomer>().SingleOrDefaultAsync(value => value.TenantId == tenantId && value.Id == id, ct);
        if (row is null || expectedVersion is not { } version) return row;
        if (row.Version != version) throw new ConcurrencyConflictException();
        context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }

    public void Add(WhatsAppCustomer customer) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Set<WhatsAppCustomer>().Add(customer); }
}
