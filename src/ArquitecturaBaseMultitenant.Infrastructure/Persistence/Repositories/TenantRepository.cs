using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class TenantRepository(ApplicationDbContext context) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.Tenants.SingleOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken);
    }

    public void Add(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        context.RequireTransaction();
        context.Tenants.Add(tenant);
    }
}
