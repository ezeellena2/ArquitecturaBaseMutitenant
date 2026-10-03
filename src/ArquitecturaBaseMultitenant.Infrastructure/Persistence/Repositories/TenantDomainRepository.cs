using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara altas y cambios de dominios globales dentro de la UoW de configuración.</summary>
internal sealed class TenantDomainRepository(ApplicationDbContext context, ITenantContext tenantContext)
    : ITenantDomainRepository
{
    public Task LockDomainAsync(OrganizationEmailDomain domain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.TenantDomain(domain.Value)], cancellationToken);
    }

    public Task<TenantDomain?> GetByIdAsync(Guid domainId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var tenantId = tenantContext.RequiredTenantId;
        return context.TenantDomains.SingleOrDefaultAsync(domain => domain.OrganizationId == tenantId
            && domain.Id == domainId && domain.Status != TenantDomainStatus.Removed, cancellationToken);
    }

    public async Task<TenantDomain?> GetByIdAsync(Guid domainId, uint expectedVersion,
        CancellationToken cancellationToken)
    {
        var domain = await GetByIdAsync(domainId, cancellationToken);
        if (domain is not null)
            context.Entry(domain).Property(candidate => candidate.Version).OriginalValue = expectedVersion;
        return domain;
    }

    public Task<TenantDomain?> FindActiveByDomainAsync(OrganizationEmailDomain domain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);
        context.RequireTransaction();
        return context.TenantDomains.SingleOrDefaultAsync(row => row.Domain == domain.Value
            && row.Status != TenantDomainStatus.Removed, cancellationToken);
    }

    public Task<Guid?> FindVerifiedOrganizationIdAsync(OrganizationEmailDomain domain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);
        context.RequireTransaction();
        return context.TenantDomains.AsNoTracking()
            .Where(row => row.Domain == domain.Value && row.Status == TenantDomainStatus.Verified)
            .Select(row => (Guid?)row.OrganizationId).SingleOrDefaultAsync(cancellationToken);
    }

    public void Add(TenantDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        if (domain.OrganizationId != tenantContext.RequiredTenantId)
            throw new InvalidOperationException("An organization can only add its own email domain.");
        context.TenantDomains.Add(domain);
    }

    public void MarkUpdated(TenantDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Entry(domain).State = EntityState.Modified;
    }
}
