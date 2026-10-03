using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Comprueba que un replay de provisionamiento coincida con el espacio y sus datos iniciales.</summary>
public sealed class TenantProvisioningReplayVerifier(ITenantRepository tenants, IMemberRepository members,
    IRoleRepository roles, IRoleAssignmentRepository assignments, ICompanyMembershipRepository companyMemberships,
    ITenantSettingsRepository settings)
{
    internal Task<Tenant?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        tenants.GetByIdAsync(tenantId, cancellationToken);

    internal async Task<Result> VerifyReplayAsync(Tenant existing, Tenant requested,
        TenantSettings requestedSettings, IReadOnlyCollection<Guid> userIds, Guid? businessOwnerUserId,
        Guid? primaryCompanyId, CancellationToken cancellationToken)
    {
        if (!Matches(existing, requested)) return TenantErrors.ProvisioningConflict;
        foreach (var userId in userIds)
        {
            var member = await members.GetByUserIdAsync(userId, cancellationToken);
            if (member is null || member.Status != MemberStatus.Active)
                return TenantErrors.ProvisioningConflict;
        }

        if (primaryCompanyId is { } companyId)
        {
            var ownerId = businessOwnerUserId ?? userIds.First();
            var owner = await members.GetByUserIdAsync(ownerId, cancellationToken);
            if (owner is null) return TenantErrors.ProvisioningConflict;
            var membership = await companyMemberships.FindAsync(companyId, owner.Id, cancellationToken);
            var companyAdmin = await roles.GetByNameAsync(SystemRoles.CompanyAdmin, cancellationToken);
            var ownerAssignments = await assignments.ListForMemberAsync(owner.Id, cancellationToken);
            if (membership is not { Status: CompanyMembershipStatus.Active } || companyAdmin is null
                || !ownerAssignments.Any(assignment => assignment.RoleId == companyAdmin.Id
                    && assignment.CompanyId == companyId))
                return TenantErrors.ProvisioningConflict;
        }

        var existingSettings = await settings.GetCurrentAsync(cancellationToken);
        return existingSettings is not null
            && existingSettings.DefaultCulture == requestedSettings.DefaultCulture
            && existingSettings.DefaultTimeZoneId == requestedSettings.DefaultTimeZoneId
            && existingSettings.DefaultCurrency == requestedSettings.DefaultCurrency
            ? Result.Success()
            : TenantErrors.ProvisioningConflict;
    }

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
