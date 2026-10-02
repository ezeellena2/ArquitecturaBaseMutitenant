using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Serializa cambios y evita dejar una empresa sin un Administrador efectivo.</summary>
internal sealed class LastCompanyAdminGuard(ICompanyAdminReader admins, ICompanyAdminLock locks,
    ITenantContext tenantContext) : ILastCompanyAdminGuard
{
    public Task LockAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null || tenantContext.TenantKind != TenantKind.Business)
            throw new InvalidOperationException("Company administration requires an active business organization.");
        return locks.LockAssignmentsAsync(companyId, cancellationToken);
    }

    public async Task<Result> CheckAnotherActiveAdminAsync(Guid companyId, CancellationToken cancellationToken) =>
        await admins.CountActiveCompanyAdminsAsync(companyId, cancellationToken) <= 1
            ? CompanyMembershipErrors.LastCompanyAdmin
            : Result.Success();
}
