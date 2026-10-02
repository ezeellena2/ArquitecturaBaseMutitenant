using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class CompanyMembershipRepository(ApplicationDbContext context, ITenantContext tenantContext)
    : ICompanyMembershipRepository
{
    public async Task<CompanyMembership?> GetByIdAsync(Guid companyId, Guid memberId, uint expectedVersion,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        var membership = await context.CompanyMemberships.SingleOrDefaultAsync(row => row.CompanyId == companyId
            && row.MemberId == memberId, cancellationToken);
        if (membership is not null)
            context.Entry(membership).Property(candidate => candidate.Version).OriginalValue = expectedVersion;
        return membership;
    }

    public Task<CompanyMembership?> FindAsync(Guid companyId, Guid memberId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        return context.CompanyMemberships.SingleOrDefaultAsync(row => row.CompanyId == companyId
            && row.MemberId == memberId, cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyMembership>> ListForMemberAsync(Guid memberId,
        CancellationToken cancellationToken)
    {
        if (memberId == Guid.Empty) throw new ArgumentException("The member id cannot be empty.", nameof(memberId));
        context.RequireTransaction();
        var tenantId = tenantContext.RequiredTenantId;
        return await context.CompanyMemberships.Where(row => row.TenantId == tenantId
            && row.MemberId == memberId).ToArrayAsync(cancellationToken);
    }

    public void Add(CompanyMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.CompanyMemberships.Add(membership);
    }

    public void MarkUpdated(CompanyMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Entry(membership).State = EntityState.Modified;
    }

    public void Remove(CompanyMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.CompanyMemberships.Remove(membership);
    }
}
