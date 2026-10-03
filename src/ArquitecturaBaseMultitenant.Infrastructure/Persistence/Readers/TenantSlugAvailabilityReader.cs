using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class TenantSlugAvailabilityReader(ApplicationDbContext context) : ITenantSlugAvailabilityReader
{
    public Task<bool> IsReservedAsync(string normalizedSlug, CancellationToken cancellationToken) =>
        context.Set<ReservedTenantSlug>()
            .AnyAsync(item => item.Slug == normalizedSlug, cancellationToken);

    public Task<bool> IsTakenAsync(string normalizedSlug, CancellationToken cancellationToken) =>
        context.Tenants.AnyAsync(tenant => tenant.Slug == normalizedSlug, cancellationToken);

    public Task<bool> IsTakenByOtherAsync(string normalizedSlug, Guid tenantId,
        CancellationToken cancellationToken) => context.Tenants.AnyAsync(
        tenant => tenant.Slug == normalizedSlug && tenant.Id != tenantId, cancellationToken);
}
