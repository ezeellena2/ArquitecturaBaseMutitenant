using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class TenantReader(ApplicationDbContext context) : ITenantReader
{
    public Task<TenantRow?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.Tenants.AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new TenantRow(tenant.Id, tenant.Kind, tenant.Status, tenant.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TenantRow>> ListActiveBusinessesAsync(CancellationToken cancellationToken) =>
        await context.Tenants.AsNoTracking()
            .Where(tenant => tenant.Kind == TenantKind.Business && tenant.Status == TenantStatus.Active)
            .OrderBy(tenant => tenant.Id)
            .Select(tenant => new TenantRow(tenant.Id, tenant.Kind, tenant.Status, tenant.Name))
            .ToArrayAsync(cancellationToken);
}
