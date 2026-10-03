using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Expone únicamente los dominios activos de la organización técnica seleccionada.</summary>
internal sealed class TenantDomainReader(ApplicationDbContext context, ITenantContext tenantContext)
    : ITenantDomainReader
{
    public async Task<IReadOnlyList<OrganizationDomainRow>> ListCurrentAsync(CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.RequiredTenantId;
        return await context.TenantDomains.AsNoTracking()
            .Where(domain => domain.OrganizationId == organizationId && domain.Status != TenantDomainStatus.Removed)
            .OrderBy(domain => domain.Domain)
            .Select(domain => new OrganizationDomainRow(domain.Id, domain.Domain, domain.Status,
                domain.VerificationToken, domain.CreatedAtUtc, domain.VerifiedAtUtc, domain.Version))
            .ToArrayAsync(cancellationToken);
    }

    public Task<OrganizationDomainRow?> FindCurrentByIdAsync(Guid domainId,
        CancellationToken cancellationToken)
    {
        if (domainId == Guid.Empty) throw new ArgumentException("A domain ID is required.", nameof(domainId));
        var organizationId = tenantContext.RequiredTenantId;
        return context.TenantDomains.AsNoTracking()
            .Where(domain => domain.OrganizationId == organizationId && domain.Id == domainId
                && domain.Status != TenantDomainStatus.Removed)
            .Select(domain => new OrganizationDomainRow(domain.Id, domain.Domain, domain.Status,
                domain.VerificationToken, domain.CreatedAtUtc, domain.VerifiedAtUtc, domain.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
