using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Verifica o retira un dominio de correo de una organización con la lógica de E6. Registra la entrada antes
/// de entrar al alcance, consulta el DNS sin transacción y confirma el cambio con su SecurityEvent.
/// </summary>
internal sealed class PlatformDomainService(ITenantReader tenants, ITenantDomainReader domains,
    PlatformActionGuard guard, OrganizationDomainVerifier verifier, OrganizationDomainRemover remover,
    PlatformTenantAudit audit, IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock,
    ILogger<PlatformDomainService> logger) : IPlatformDomainService
{
    public Task<Result<IReadOnlyList<OrganizationDomainResponse>>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<IReadOnlyList<OrganizationDomainResponse>>(logger, clock, "ListPlatformDomains", async () =>
        {
            var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
            if (tenant is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
            var entered = await guard.EnterAsync(tenantId, AuditTexts.DomainMetadataReadReason(), cancellationToken);
            if (entered.IsFailure) return entered.Error;
            using var lease = entered.Value;
            return (await domains.ListCurrentAsync(cancellationToken)).Select(OrganizationDomainService.ToResponse).ToArray();
        });

    public Task<Result> VerifyAsync(Guid tenantId, string domain, PlatformTenantStateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "VerifyPlatformDomain", () =>
            ChangeAsync(tenantId, domain, request, verify: true, cancellationToken));

    public Task<Result> RemoveAsync(Guid tenantId, string domain, PlatformTenantStateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "RemovePlatformDomain", () =>
            ChangeAsync(tenantId, domain, request, verify: false, cancellationToken));

    private async Task<Result> ChangeAsync(Guid tenantId, string domain, PlatformTenantStateRequest request,
        bool verify, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
        var normalized = OrganizationEmailDomain.Create(domain);
        if (normalized.IsFailure) return normalized.Error;
        var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is not { Kind: TenantKind.Business }) return TenantErrors.NotFound;
        var entered = await guard.EnterAsync(tenantId, request.Reason!, cancellationToken);
        if (entered.IsFailure) return entered.Error;
        using var lease = entered.Value;
        var current = (await domains.ListCurrentAsync(cancellationToken))
            .SingleOrDefault(row => row.Domain == normalized.Value.Value);
        if (current is null) return TenantDomainErrors.NotFound;
        if (current.Version != request.Version!.Value) throw new ConcurrencyConflictException();
        if (verify && await verifier.PrepareAsync(current, cancellationToken) is { IsFailure: true } unverified)
            return unverified;
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var changed = verify ? await verifier.StageAsync(current, ct)
                : await remover.StageAsync(current, request.Version.Value, ct);
            if (changed.IsFailure) return changed;
            var changes = new SecurityEventChanges();
            changes.AddBoolean("domain.verified", current.Status == TenantDomainStatus.Verified, verify);
            if (!verify) changes.AddBoolean("domain.present", true, false);
            audit.Record(tenantId, verify ? SecurityEventType.OrganizationDomainVerified
                : SecurityEventType.OrganizationDomainRemoved, request.Reason!, changes);
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
    }
}
