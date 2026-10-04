using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Impide quitar al último Dueño o Administrador activo durante cambios de membresía.</summary>
internal sealed class OrganizationUserAdministratorGuard(
    IRoleRepository roles,
    ILastTenantAdminGuard lastTenantAdmin,
    ILastCompanyAdminGuard lastCompanyAdmin,
    ICompanyReader companies)
{
    internal async Task<Result> ProtectAsync(Member member, UserStatus? userStatus,
        OrganizationUserAssignmentSnapshot snapshot, DesiredOrganizationUserAssignments? desired,
        bool deactivating, bool removing, CancellationToken cancellationToken)
    {
        var memberIsActive = member.Status == MemberStatus.Active && userStatus == UserStatus.Active;
        var ownerRole = await roles.GetByNameAsync(SystemRoles.TenantAdmin, cancellationToken);
        var keepsOwner = !deactivating && !removing && desired is not null && ownerRole is not null
            && desired.OrganizationRoles.Any(row => row.Id == ownerRole.Id);
        if (memberIsActive && ownerRole is not null
            && snapshot.Assignments.Any(row => row.RoleId == ownerRole.Id && row.CompanyId is null)
            && !keepsOwner && member.UserId is { } ownerId)
        {
            var canLeave = await lastTenantAdmin.CheckAnotherOwnerAsync(member.TenantId, ownerId, cancellationToken);
            if (canLeave.IsFailure) return canLeave;
        }

        var companyAdmin = await roles.GetByNameAsync(SystemRoles.CompanyAdmin, cancellationToken);
        if (companyAdmin is null) return Result.Success();
        var oldAdminCompanyIds = snapshot.Assignments.Where(row => row.RoleId == companyAdmin.Id
                && row.CompanyId is not null).Select(row => row.CompanyId!.Value).Distinct().Order().ToArray();
        var newAdminCompanyIds = desired is null ? [] : desired.CompanyRoles
            .Where(pair => pair.Value.Roles.Any(role => role.Id == companyAdmin.Id))
            .Select(pair => pair.Key).Order().ToArray();
        foreach (var companyId in oldAdminCompanyIds.Union(newAdminCompanyIds).Distinct().Order())
            await lastCompanyAdmin.LockAsync(companyId, cancellationToken);

        if (!memberIsActive) return Result.Success();
        var membershipByCompany = snapshot.CompanyMemberships.ToDictionary(row => row.CompanyId);
        foreach (var companyId in oldAdminCompanyIds)
        {
            var membership = membershipByCompany.GetValueOrDefault(companyId);
            if (membership?.Status != CompanyMembershipStatus.Active) continue;
            if (!deactivating && !removing && newAdminCompanyIds.Contains(companyId)) continue;
            var company = await companies.FindByIdAsync(companyId, cancellationToken);
            if (company?.Status != CompanyStatus.Active) continue;
            var canLeave = await lastCompanyAdmin.CheckAnotherActiveAdminAsync(companyId, cancellationToken);
            if (canLeave.IsFailure) return canLeave;
        }
        return Result.Success();
    }
}
