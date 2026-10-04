using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>
/// Escribe membresías de empresa dentro de la transacción del servicio, bajo el lock que protege al último
/// Administrador.
/// </summary>
internal sealed class CompanyMembershipWriter(
    ICompanyReader companies,
    ICompanyMembershipRepository memberships,
    IMemberRepository members,
    IMemberReader memberReader,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    RoleAssignmentGuard roleGuard,
    ILastCompanyAdminGuard lastCompanyAdmin)
{
    public async Task<Result> AddAsync(AddCompanyMemberRequest request, CancellationToken ct)
    {
        var active = await LockActiveCompanyAsync(request.CompanyId, ct);
        if (active.IsFailure) return active;
        var checkedRoles = await roleGuard.CheckAsync(request.RoleIds!, request.CompanyId, ct);
        if (checkedRoles.IsFailure) return checkedRoles.Error;
        var member = await members.GetByIdAsync(request.MemberId!.Value, ct);
        if (member is null) return CompanyMembershipErrors.NotFound;
        if (member.Status is not (MemberStatus.Active or MemberStatus.Invited))
            return CompanyMembershipErrors.MemberInactive;
        if (await memberships.FindAsync(request.CompanyId, member.Id, ct) is not null)
            return CompanyMembershipErrors.AlreadyExists;

        var assignmentsToAdd = RoleAssignmentGuard.Assign(member.Id, checkedRoles.Value, request.CompanyId);
        if (assignmentsToAdd.IsFailure) return assignmentsToAdd.Error;
        var membership = CompanyMembership.Create(request.CompanyId, member.Id);
        if (membership.IsFailure) return membership.Error;
        memberships.Add(membership.Value);
        foreach (var assignment in assignmentsToAdd.Value) assignments.Add(assignment);
        return Result.Success();
    }

    public async Task<Result> UpdateRolesAsync(UpdateCompanyMemberRolesRequest request, CancellationToken ct)
    {
        var active = await LockActiveCompanyAsync(request.CompanyId, ct);
        if (active.IsFailure) return active;
        var checkedRoles = await roleGuard.CheckAsync(request.RoleIds!, request.CompanyId, ct);
        if (checkedRoles.IsFailure) return checkedRoles.Error;
        var desiredIds = checkedRoles.Value.Select(role => role.Id).ToHashSet();
        var membership = await memberships.GetByIdAsync(request.CompanyId, request.MemberId,
            request.Version!.Value, ct);
        if (membership is null) return CompanyMembershipErrors.NotFound;
        if (membership.Status != CompanyMembershipStatus.Active) return CompanyMembershipErrors.MemberInactive;
        var member = await members.GetByIdAsync(membership.MemberId, ct);
        if (member is null || member.Status != MemberStatus.Active)
            return CompanyMembershipErrors.MemberInactive;

        var current = await assignments.ListForMemberAsync(member.Id, ct);
        var companyAssignments = current.Where(assignment => assignment.CompanyId == request.CompanyId).ToArray();
        var companyAdmin = await roles.GetByNameAsync(SystemRoles.CompanyAdmin, ct);
        var removesLastAdmin = companyAdmin is not null
            && companyAssignments.Any(assignment => assignment.RoleId == companyAdmin.Id)
            && !desiredIds.Contains(companyAdmin.Id);
        if (removesLastAdmin)
        {
            var canRemove = await lastCompanyAdmin.CheckAnotherActiveAdminAsync(request.CompanyId, ct);
            if (canRemove.IsFailure) return canRemove;
        }

        var desired = RoleAssignmentGuard.Assign(member.Id, checkedRoles.Value, request.CompanyId);
        if (desired.IsFailure) return desired.Error;
        foreach (var existing in companyAssignments.Where(assignment => !desiredIds.Contains(assignment.RoleId)))
            assignments.Remove(existing);
        var existingIds = companyAssignments.Select(assignment => assignment.RoleId).ToHashSet();
        foreach (var assignment in desired.Value.Where(assignment => !existingIds.Contains(assignment.RoleId)))
            assignments.Add(assignment);
        memberships.MarkUpdated(membership);
        return Result.Success();
    }

    public async Task<Result> ChangeStatusAsync(ChangeCompanyMemberStatusRequest request, CancellationToken ct)
    {
        var active = await LockActiveCompanyAsync(request.CompanyId, ct);
        if (active.IsFailure) return active;
        var membership = await memberships.GetByIdAsync(request.CompanyId, request.MemberId,
            request.Version!.Value, ct);
        if (membership is null) return CompanyMembershipErrors.NotFound;
        var member = await members.GetByIdAsync(membership.MemberId, ct);
        if (member is null) return CompanyMembershipErrors.NotFound;
        var targetStatus = request.Status!.Value;
        if (targetStatus == CompanyMembershipStatus.Active
            && member.Status is not (MemberStatus.Active or MemberStatus.Invited))
            return CompanyMembershipErrors.MemberInactive;
        if (targetStatus == CompanyMembershipStatus.Active && member.UserId is { } userId)
        {
            var user = await memberReader.FindByUserIdAsync(userId, ct);
            if (user is null || user.UserStatus != UserStatus.Active)
                return CompanyMembershipErrors.MemberInactive;
        }

        if (targetStatus == CompanyMembershipStatus.Inactive)
        {
            var current = await assignments.ListForMemberAsync(member.Id, ct);
            var companyAssignments = current.Where(assignment => assignment.CompanyId == request.CompanyId).ToArray();
            var companyAdmin = await roles.GetByNameAsync(SystemRoles.CompanyAdmin, ct);
            if (companyAdmin is not null && companyAssignments.Any(assignment => assignment.RoleId == companyAdmin.Id))
            {
                var canRemove = await lastCompanyAdmin.CheckAnotherActiveAdminAsync(request.CompanyId, ct);
                if (canRemove.IsFailure) return canRemove;
            }

            foreach (var assignment in companyAssignments) assignments.Remove(assignment);
        }

        membership.SetStatus(targetStatus);
        memberships.MarkUpdated(membership);
        return Result.Success();
    }

    // «La empresa existe y está activa» se controla una sola vez, con el lock del último Administrador tomado.
    private async Task<Result> LockActiveCompanyAsync(Guid companyId, CancellationToken ct)
    {
        await lastCompanyAdmin.LockAsync(companyId, ct);
        var company = await companies.FindByIdAsync(companyId, ct);
        if (company is null) return CompanyErrors.NotFound;
        return company.Status == CompanyStatus.Active ? Result.Success() : CompanyErrors.Inactive;
    }
}
