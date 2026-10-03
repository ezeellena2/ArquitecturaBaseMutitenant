using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara tenant, miembros iniciales, roles y configuración de forma idempotente.</summary>
public sealed class TenantProvisioner(
    TenantSpaceProvisioner newTenantProvisioner,
    TenantProvisioningReplayVerifier replayVerifier,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    ILogger<TenantProvisioner> logger)
{
    /// <summary>Prepara una organización para un Dueño activo o invitado, sin crear identidad ni Personal.</summary>
    public Task<Result> StageBusinessOwnerAsync(Tenant tenant, TenantSettings settings, Member owner,
        CancellationToken cancellationToken, Guid? primaryCompanyId = null) =>
        OperationLog.RunAsync(logger, timeProvider, "ProvisionBusinessOwner", async () =>
        {
            ArgumentNullException.ThrowIfNull(tenant);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(owner);
            if (tenant.Kind != TenantKind.Business || tenantContext.TenantId != tenant.Id
                || tenant.Status is not (TenantStatus.Active or TenantStatus.PendingApproval)
                || owner.Status is not (MemberStatus.Invited or MemberStatus.Active)
                || (owner.TenantId != Guid.Empty && owner.TenantId != tenant.Id))
                return TenantErrors.ProvisioningConflict;
            if (primaryCompanyId == Guid.Empty) return TenantErrors.ProvisioningConflict;
            var existing = await replayVerifier.FindTenantAsync(tenant.Id, cancellationToken);
            if (existing is not null && (existing.Kind != tenant.Kind || existing.Name != tenant.Name
                || existing.Slug != tenant.Slug || existing.Status != tenant.Status))
                return TenantErrors.ProvisioningConflict;
            return await newTenantProvisioner.StageBusinessOwnerAsync(tenant, settings, owner,
                existing is null, primaryCompanyId, cancellationToken);
        });

    public Task<Result> StageAsync(Tenant tenant, TenantSettings tenantSettings,
        IReadOnlyCollection<Guid> userIds, Guid? businessOwnerUserId, CancellationToken cancellationToken,
        Guid? primaryCompanyId = null) =>
        OperationLog.RunAsync(logger, timeProvider, "ProvisionTenant", async () =>
        {
            ArgumentNullException.ThrowIfNull(tenant);
            ArgumentNullException.ThrowIfNull(tenantSettings);
            ArgumentNullException.ThrowIfNull(userIds);
            if (userIds.Count == 0)
                throw new ArgumentException("A new space needs an initial member.", nameof(userIds));
            if (tenant.Kind != TenantKind.Business && businessOwnerUserId is not null)
                throw new ArgumentException("Only a business space can specify an owner.", nameof(businessOwnerUserId));
            if (tenant.Kind != TenantKind.Business && primaryCompanyId is not null)
                throw new ArgumentException("Only a business space can have an initial company.", nameof(primaryCompanyId));
            if (primaryCompanyId == Guid.Empty)
                throw new ArgumentException("The initial company ID is required.", nameof(primaryCompanyId));
            if (businessOwnerUserId is { } requestedOwner && !userIds.Contains(requestedOwner))
                throw new ArgumentException("The owner must be one of the initial members.", nameof(businessOwnerUserId));
            if (tenant.Kind == TenantKind.Business && userIds.Count > 1 && businessOwnerUserId is null)
                throw new ArgumentException("A business space with multiple initial members needs an explicit owner.",
                    nameof(businessOwnerUserId));
            if (tenantContext.TenantId != tenant.Id)
                return TenantErrors.ProvisioningConflict;

            var existing = await replayVerifier.FindTenantAsync(tenant.Id, cancellationToken);
            if (existing is null)
            {
                newTenantProvisioner.Stage(tenant, tenantSettings, userIds, businessOwnerUserId, primaryCompanyId);
                return Result.Success();
            }

            return await replayVerifier.VerifyReplayAsync(existing, tenant, tenantSettings, userIds,
                businessOwnerUserId, primaryCompanyId, cancellationToken);
        });
}
