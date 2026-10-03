using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class PlatformTenantRepository(ApplicationDbContext context) : IPlatformTenantRepository
{
    public async Task<Tenant?> GetForUpdateAsync(Guid id, uint version, CancellationToken ct)
    {
        context.RequireTransaction();
        if (id == Guid.Empty) return null;
        await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.For(id, "platform-administration", "state")], ct);
        var tenant = await context.Tenants.SingleOrDefaultAsync(t => t.Id == id && t.Kind == TenantKind.Business, ct);
        if (tenant is not null) context.Entry(tenant).Property(t => t.Version).OriginalValue = version;
        return tenant;
    }

    public void MarkUpdated(Tenant tenant)
    {
        context.RequireTransaction();
        context.Entry(tenant).Property(t => t.Status).IsModified = true;
    }
}
