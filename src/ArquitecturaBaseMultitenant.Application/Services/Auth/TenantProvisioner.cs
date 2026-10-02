using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara tenant, miembros iniciales, roles y configuración de forma idempotente.</summary>
public sealed class TenantProvisioner(
    ITenantRepository tenants,
    IMemberRepository members,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    ITenantSettingsRepository settings,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    ILogger<TenantProvisioner> logger)
{
    public Task<Result> StageAsync(Tenant tenant, TenantSettings tenantSettings,
        IReadOnlyCollection<Guid> userIds, Guid? businessOwnerUserId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "ProvisionTenant", async () =>
        {
            ArgumentNullException.ThrowIfNull(tenant);
            ArgumentNullException.ThrowIfNull(tenantSettings);
            ArgumentNullException.ThrowIfNull(userIds);
            if (userIds.Count == 0)
                throw new ArgumentException("A new space needs an initial member.", nameof(userIds));
            if (tenant.Kind != TenantKind.Business && businessOwnerUserId is not null)
                throw new ArgumentException("Only a business space can specify an owner.", nameof(businessOwnerUserId));
            if (businessOwnerUserId is { } requestedOwner && !userIds.Contains(requestedOwner))
                throw new ArgumentException("The owner must be one of the initial members.", nameof(businessOwnerUserId));
            if (tenant.Kind == TenantKind.Business && userIds.Count > 1 && businessOwnerUserId is null)
                throw new ArgumentException("A business space with multiple initial members needs an explicit owner.",
                    nameof(businessOwnerUserId));
            if (tenantContext.TenantId != tenant.Id)
                return TenantErrors.ProvisioningConflict;

            var existing = await tenants.GetByIdAsync(tenant.Id, cancellationToken);
            if (existing is null)
            {
                new TenantSpaceProvisioner(tenants, members, roles, assignments, settings, timeProvider)
                    .Stage(tenant, tenantSettings, userIds, businessOwnerUserId);
                return Result.Success();
            }

            if (!Matches(existing, tenant)) return TenantErrors.ProvisioningConflict;
            foreach (var userId in userIds)
            {
                var member = await members.GetByUserIdAsync(userId, cancellationToken);
                if (member is null || member.Status != MemberStatus.Active)
                    return TenantErrors.ProvisioningConflict;
            }

            var existingSettings = await settings.GetCurrentAsync(cancellationToken);
            return existingSettings is not null
                && existingSettings.DefaultCulture == tenantSettings.DefaultCulture
                && existingSettings.DefaultTimeZoneId == tenantSettings.DefaultTimeZoneId
                && existingSettings.DefaultCurrency == tenantSettings.DefaultCurrency
                ? Result.Success()
                : TenantErrors.ProvisioningConflict;
        });

    private static bool Matches(Tenant existing, Tenant requested)
    {
        if (existing.Kind != requested.Kind || existing.Name != requested.Name || existing.Slug != requested.Slug)
            return false;

        var expectedStatus = requested.Status == TenantStatus.PendingApproval
            ? TenantStatus.PendingApproval
            : TenantStatus.Active;
        return existing.Status == expectedStatus;
    }
}
