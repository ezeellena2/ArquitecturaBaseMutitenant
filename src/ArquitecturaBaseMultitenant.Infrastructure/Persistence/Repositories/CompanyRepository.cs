using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class CompanyRepository(ApplicationDbContext context, ITenantContext tenantContext)
    : ICompanyRepository
{
    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        return context.Companies.SingleOrDefaultAsync(row => row.Id == companyId, cancellationToken);
    }

    public async Task<Company?> GetByIdAsync(Guid companyId, uint expectedVersion,
        CancellationToken cancellationToken)
    {
        var company = await GetByIdAsync(companyId, cancellationToken);
        if (company is not null)
            context.Entry(company).Property(candidate => candidate.Version).OriginalValue = expectedVersion;
        return company;
    }

    public void Add(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Companies.Add(company);
    }

    public void MarkUpdated(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Entry(company).State = EntityState.Modified;
    }

    public void Remove(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Companies.Remove(company);
    }
}
