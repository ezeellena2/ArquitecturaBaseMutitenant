using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>
/// Retira el dominio dentro de la transacción del llamador, conservando ManagedByTenantId en los métodos
/// ya vinculados.
/// </summary>
internal sealed class OrganizationDomainRemover(ITenantDomainRepository domains, IAuditLog auditLog)
{
    internal async Task<Result<TenantDomain>> StageAsync(OrganizationDomainRow current, uint version,
        CancellationToken cancellationToken)
    {
        var organizationDomain = OrganizationEmailDomain.Create(current.Domain);
        if (organizationDomain.IsFailure) throw new InvalidOperationException("A stored organization domain is invalid.");
        await domains.LockDomainAsync(organizationDomain.Value, cancellationToken);
        var domain = await domains.GetByIdAsync(current.Id, version, cancellationToken);
        if (domain is null) return TenantDomainErrors.NotFound;
        var removed = domain.Remove();
        if (removed.IsFailure) return removed.Error;
        domains.MarkUpdated(domain);
        auditLog.Record(AuditAction.Updated, "TenantDomain", domain.Id,
            new Dictionary<string, object?> { ["domain"] = domain.Domain, ["status"] = domain.Status.ToString() });
        return domain;
    }
}
