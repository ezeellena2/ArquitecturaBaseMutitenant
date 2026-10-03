using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Dns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Consulta el TXT y asocia métodos de correo verificados bajo la versión bloqueada del dominio.</summary>
internal sealed class OrganizationDomainVerifier(ITenantDomainRepository domains,
    ILoginMethodRepository loginMethods, IDnsTxtLookup dns, IAuditLog auditLog,
    IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    internal async Task<Result> VerifyAsync(OrganizationDomainRow pending, CancellationToken cancellationToken)
    {
        var organizationDomain = OrganizationEmailDomain.Create(pending.Domain);
        if (organizationDomain.IsFailure) throw new InvalidOperationException("A stored organization domain is invalid.");
        var records = await dns.FindTxtRecordsAsync(pending.Domain, cancellationToken);
        if (records.IsFailure) return records.Error;
        var expected = TenantDomain.TxtRecordPrefix + pending.VerificationToken;
        if (!records.Value.Contains(expected, StringComparer.Ordinal)) return TenantDomainErrors.TxtNotFound;

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await domains.LockDomainAsync(organizationDomain.Value, ct);
            var domain = await domains.GetByIdAsync(pending.Id, pending.Version, ct);
            if (domain is null) return TenantDomainErrors.NotFound;
            var verified = domain.Verify(timeProvider.GetUtcNow().UtcDateTime);
            if (verified.IsFailure) return verified;
            domains.MarkUpdated(domain);
            var associated = await loginMethods.ListVerifiedByEmailDomainAsync(organizationDomain.Value, ct);
            foreach (var method in associated) method.AssignManagedByOrganization(domain.OrganizationId);
            auditLog.Record(AuditAction.Updated, "TenantDomain", domain.Id,
                new Dictionary<string, object?> { ["domain"] = domain.Domain, ["status"] = domain.Status.ToString() });
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
    }
}
