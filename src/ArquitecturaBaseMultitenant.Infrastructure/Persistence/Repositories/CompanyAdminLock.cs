using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Adquiere un lock transaccional por empresa antes de tocar sus asignaciones de Administrador.</summary>
internal sealed class CompanyAdminLock(ApplicationDbContext context, ITenantContext tenantContext)
    : ICompanyAdminLock
{
    public Task LockAssignmentsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("A company id is required.", nameof(companyId));
        var tenantId = tenantContext.RequiredTenantId;
        return context.AcquireAdvisoryLocksAsync(
            [AdvisoryLockKeys.For(tenantId, "company-admin-assignments", companyId)], cancellationToken);
    }
}
