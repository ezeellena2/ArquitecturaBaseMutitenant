using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Dns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>
/// Consulta el TXT fuera de la transacción y, dentro de la del llamador, asocia los métodos de correo
/// verificados bajo la versión bloqueada del dominio.
/// </summary>
internal sealed class OrganizationDomainVerifier(ITenantDomainRepository domains,
    ILoginMethodRepository loginMethods, IDnsTxtLookup dns, IAuditLog auditLog, TimeProvider timeProvider)
{
    internal async Task<Result> PrepareAsync(OrganizationDomainRow pending, CancellationToken cancellationToken)
    {
        if (pending.Status == TenantDomainStatus.Verified) return TenantDomainErrors.AlreadyVerified;
        var records = await dns.FindTxtRecordsAsync(pending.Domain, cancellationToken);
        if (records.IsFailure) return records.Error;
        var expected = TenantDomain.TxtRecordPrefix + pending.VerificationToken;
        return records.Value.Contains(expected, StringComparer.Ordinal)
            ? Result.Success() : TenantDomainErrors.TxtNotFound;
    }

    internal async Task<Result<TenantDomain>> StageAsync(OrganizationDomainRow pending, CancellationToken cancellationToken)
    {
        var organizationDomain = OrganizationEmailDomain.Create(pending.Domain);
        if (organizationDomain.IsFailure) throw new InvalidOperationException("A stored organization domain is invalid.");
        await domains.LockDomainAsync(organizationDomain.Value, cancellationToken);
        var domain = await domains.GetByIdAsync(pending.Id, pending.Version, cancellationToken);
        if (domain is null) return TenantDomainErrors.NotFound;
        var verified = domain.Verify(timeProvider.GetUtcNow().UtcDateTime);
        if (verified.IsFailure) return verified.Error;
        domains.MarkUpdated(domain);
        var associated = await loginMethods.ListVerifiedByEmailDomainAsync(organizationDomain.Value, cancellationToken);
        foreach (var method in associated) method.AssignManagedByOrganization(domain.OrganizationId);
        auditLog.Record(AuditAction.Updated, nameof(TenantDomain), domain.Id,
            new Dictionary<string, object?> { ["domain"] = domain.Domain, ["status"] = domain.Status.ToString() });
        return domain;
    }
}
