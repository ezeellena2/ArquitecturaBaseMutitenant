using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Retira el dominio conservando ManagedByTenantId en los métodos ya vinculados.</summary>
internal sealed class OrganizationDomainRemover(ITenantDomainRepository domains,
    IAuditLog auditLog, IUnitOfWork unitOfWork)
{
    internal async Task<Result> RemoveAsync(RemoveOrganizationDomainRequest request,
        OrganizationDomainRow current, CancellationToken cancellationToken)
    {
        var organizationDomain = OrganizationEmailDomain.Create(current.Domain);
        if (organizationDomain.IsFailure) throw new InvalidOperationException("A stored organization domain is invalid.");
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await domains.LockDomainAsync(organizationDomain.Value, ct);
            var domain = await domains.GetByIdAsync(request.DomainId, request.Version!.Value, ct);
            if (domain is null) return TenantDomainErrors.NotFound;
            var removed = domain.Remove();
            if (removed.IsFailure) return removed;
            domains.MarkUpdated(domain);
            auditLog.Record(AuditAction.Updated, "TenantDomain", domain.Id,
                new Dictionary<string, object?> { ["domain"] = domain.Domain, ["status"] = domain.Status.ToString() });
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
    }
}
