using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Dns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Registra y verifica dominios de correo globales sin modificar la identidad ni los accesos B2B.</summary>
internal sealed class OrganizationDomainService(ITenantDomainReader reader, ITenantDomainRepository domains,
    ILoginMethodRepository loginMethods, IDnsTxtLookup dns, ISecureTokenGenerator tokens,
    ITenantContext tenantContext, IAuditLog auditLog, IRequestValidator validator, IUnitOfWork unitOfWork,
    TimeProvider timeProvider, ILogger<OrganizationDomainService> logger) : IOrganizationDomainService
{
    private const string DomainUniqueConstraint = "IX_TenantDomains_Domain";

    public Task<Result<IReadOnlyList<OrganizationDomainResponse>>> ListAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<IReadOnlyList<OrganizationDomainResponse>>(logger, timeProvider,
            "ListOrganizationDomains", async () =>
            {
                var rows = await reader.ListCurrentAsync(cancellationToken);
                return rows.Select(ToResponse).ToArray();
            });

    public Task<Result<OrganizationDomainResponse>> RegisterAsync(RegisterOrganizationDomainRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<OrganizationDomainResponse>(logger, timeProvider,
            "RegisterOrganizationDomain", async () =>
            {
                if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
                var domain = OrganizationEmailDomain.Create(request.Domain);
                if (domain.IsFailure) return domain.Error;
                var organizationId = tenantContext.RequiredTenantId;
                try
                {
                    return await unitOfWork.ExecuteInTransactionAsync<Result<OrganizationDomainResponse>>(async ct =>
                    {
                        await domains.LockDomainAsync(domain.Value, ct);
                        if (await domains.FindActiveByDomainAsync(domain.Value, ct) is not null)
                            return TenantDomainErrors.AlreadyExists;
                        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                        var created = TenantDomain.Create(organizationId, domain.Value, tokens.Generate(), nowUtc);
                        if (created.IsFailure) return created.Error;
                        domains.Add(created.Value);
                        auditLog.Record(AuditAction.Created, "TenantDomain", created.Value.Id,
                            new Dictionary<string, object?> { ["domain"] = created.Value.Domain, ["status"] = created.Value.Status.ToString() });
                        return ToResponse(created.Value);
                    }, CommitPolicy.OnSuccess, cancellationToken);
                }
                catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == DomainUniqueConstraint)
                {
                    return TenantDomainErrors.AlreadyExists;
                }
            });
    }

    public Task<Result> VerifyAsync(Guid domainId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "VerifyOrganizationDomain", async () =>
        {
            if (domainId == Guid.Empty) return TenantDomainErrors.NotFound;
            var pending = await reader.FindCurrentByIdAsync(domainId, cancellationToken);
            if (pending is null) return TenantDomainErrors.NotFound;
            if (pending.Status == TenantDomainStatus.Verified) return TenantDomainErrors.AlreadyVerified;
            var organizationDomain = OrganizationEmailDomain.Create(pending.Domain);
            if (organizationDomain.IsFailure) throw new InvalidOperationException("A stored organization domain is invalid.");
            var records = await dns.FindTxtRecordsAsync(pending.Domain, cancellationToken);
            if (records.IsFailure) return records.Error;
            var expected = TenantDomain.TxtRecordPrefix + pending.VerificationToken;
            if (!records.Value.Contains(expected, StringComparer.Ordinal)) return TenantDomainErrors.TxtNotFound;

            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await domains.LockDomainAsync(organizationDomain.Value, ct);
                var domain = await domains.GetByIdAsync(domainId, pending.Version, ct);
                if (domain is null) return TenantDomainErrors.NotFound;
                var verifiedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                var verified = domain.Verify(verifiedAtUtc);
                if (verified.IsFailure) return verified;
                domains.MarkUpdated(domain);
                var associated = await loginMethods.ListVerifiedByEmailDomainAsync(organizationDomain.Value, ct);
                foreach (var method in associated) method.AssignManagedByOrganization(domain.OrganizationId);
                auditLog.Record(AuditAction.Updated, "TenantDomain", domain.Id,
                    new Dictionary<string, object?> { ["domain"] = domain.Domain, ["status"] = domain.Status.ToString() });
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> RemoveAsync(RemoveOrganizationDomainRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "RemoveOrganizationDomain", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var current = await reader.FindCurrentByIdAsync(request.DomainId, cancellationToken);
            if (current is null) return TenantDomainErrors.NotFound;
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
        });
    }

    private static OrganizationDomainResponse ToResponse(TenantDomain domain) =>
        new(domain.Id, domain.Domain, domain.Status,
            domain.Status == TenantDomainStatus.Pending ? domain.Domain : null,
            domain.Status == TenantDomainStatus.Pending ? domain.TxtRecordValue : null,
            domain.CreatedAtUtc, domain.VerifiedAtUtc, domain.Version);

    private static OrganizationDomainResponse ToResponse(OrganizationDomainRow domain) =>
        new(domain.Id, domain.Domain, domain.Status,
            domain.Status == TenantDomainStatus.Pending ? domain.Domain : null,
            domain.Status == TenantDomainStatus.Pending ? TenantDomain.TxtRecordPrefix + domain.VerificationToken : null,
            domain.CreatedAtUtc, domain.VerifiedAtUtc, domain.Version);
}
