using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Registra un dominio único y crea el token de verificación TXT, dentro de la transacción del servicio.</summary>
internal sealed class OrganizationDomainRegistrar(ITenantDomainRepository domains,
    ISecureTokenGenerator tokens, ITenantContext tenantContext, IAuditLog auditLog, TimeProvider timeProvider)
{
    internal async Task<Result<OrganizationDomainResponse>> RegisterAsync(OrganizationEmailDomain domain,
        CancellationToken ct)
    {
        await domains.LockDomainAsync(domain, ct);
        if (await domains.FindActiveByDomainAsync(domain, ct) is not null)
            return TenantDomainErrors.AlreadyExists;
        var created = TenantDomain.Create(tenantContext.RequiredTenantId, domain, tokens.Generate(),
            timeProvider.GetUtcNow().UtcDateTime);
        if (created.IsFailure) return created.Error;
        domains.Add(created.Value);
        auditLog.Record(AuditAction.Created, "TenantDomain", created.Value.Id,
            new Dictionary<string, object?>
            {
                ["domain"] = created.Value.Domain,
                ["status"] = created.Value.Status.ToString(),
            });
        return ToResponse(created.Value);
    }

    private static OrganizationDomainResponse ToResponse(TenantDomain domain) =>
        new(domain.Id, domain.Domain, domain.Status,
            domain.Status == TenantDomainStatus.Pending ? domain.Domain : null,
            domain.Status == TenantDomainStatus.Pending ? domain.TxtRecordValue : null,
            domain.CreatedAtUtc, domain.VerifiedAtUtc, domain.Version, ManagedEmailCount: 0);
}
