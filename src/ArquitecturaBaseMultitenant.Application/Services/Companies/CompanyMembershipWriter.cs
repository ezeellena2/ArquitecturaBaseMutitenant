using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Ejecuta escrituras de membresía bajo el lock que protege al último Administrador.</summary>
internal sealed class CompanyMembershipWriter(
    ICompanyReader companies,
    ICompanyMembershipRepository memberships,
    IMemberRepository members,
    IMemberReader memberReader,
    CompanyMembershipRoleManager roleManager,
    ILastCompanyAdminGuard lastCompanyAdmin,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> AddAsync(AddCompanyMemberRequest request, CancellationToken cancellationToken)
    {
        var company = await companies.FindByIdAsync(request.CompanyId, cancellationToken);
        if (company is null) return CompanyErrors.NotFound;
        if (company.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
        var validRoleIds = await roleManager.ValidateRoleIdsAsync(request.CompanyId,
            request.RoleIds!.ToArray(), cancellationToken);
        if (validRoleIds.IsFailure) return validRoleIds.Error;

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await lastCompanyAdmin.LockAsync(request.CompanyId, ct);
            var currentCompany = await companies.FindByIdAsync(request.CompanyId, ct);
            if (currentCompany is null) return CompanyErrors.NotFound;
            if (currentCompany.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
            var member = await members.GetByIdAsync(request.MemberId!.Value, ct);
            if (member is null) return CompanyMembershipErrors.NotFound;
            if (member.Status is not (MemberStatus.Active or MemberStatus.Invited))
                return CompanyMembershipErrors.MemberInactive;
            if (await memberships.FindAsync(request.CompanyId, member.Id, ct) is not null)
                return CompanyMembershipErrors.AlreadyExists;

            var assignmentsToAdd = await roleManager.CreateAssignmentsAsync(member.Id, request.CompanyId,
                validRoleIds.Value, ct);
            if (assignmentsToAdd.IsFailure) return assignmentsToAdd.Error;
            var membership = CompanyMembership.Create(request.CompanyId, member.Id);
            if (membership.IsFailure) return membership.Error;
            memberships.Add(membership.Value);
            foreach (var assignment in assignmentsToAdd.Value) roleManager.Add(assignment);
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
    }

    public async Task<Result> UpdateRolesAsync(UpdateCompanyMemberRolesRequest request,
        CancellationToken cancellationToken)
    {
        var company = await companies.FindByIdAsync(request.CompanyId, cancellationToken);
        if (company is null) return CompanyErrors.NotFound;
        if (company.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
        var validRoleIds = await roleManager.ValidateRoleIdsAsync(request.CompanyId,
            request.RoleIds!.ToArray(), cancellationToken);
        if (validRoleIds.IsFailure) return validRoleIds.Error;

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await lastCompanyAdmin.LockAsync(request.CompanyId, ct);
            var currentCompany = await companies.FindByIdAsync(request.CompanyId, ct);
            if (currentCompany is null) return CompanyErrors.NotFound;
            if (currentCompany.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
            var membership = await memberships.GetByIdAsync(request.CompanyId, request.MemberId,
                request.Version!.Value, ct);
            if (membership is null) return CompanyMembershipErrors.NotFound;
            if (membership.Status != CompanyMembershipStatus.Active) return CompanyMembershipErrors.MemberInactive;
            var member = await members.GetByIdAsync(membership.MemberId, ct);
            if (member is null || member.Status != MemberStatus.Active)
                return CompanyMembershipErrors.MemberInactive;

            var current = await roleManager.ListForMemberAsync(member.Id, ct);
            var companyAssignments = current.Where(assignment => assignment.CompanyId == request.CompanyId).ToArray();
            var companyAdmin = await roleManager.GetByNameAsync(SystemRoles.CompanyAdmin, ct);
            var removesLastAdmin = companyAdmin is not null
                && companyAssignments.Any(assignment => assignment.RoleId == companyAdmin.Id)
                && !validRoleIds.Value.Contains(companyAdmin.Id);
            if (removesLastAdmin)
            {
                var canRemove = await lastCompanyAdmin.CheckAnotherActiveAdminAsync(request.CompanyId, ct);
                if (canRemove.IsFailure) return canRemove;
            }

            var desired = await roleManager.CreateAssignmentsAsync(member.Id, request.CompanyId,
                validRoleIds.Value, ct);
            if (desired.IsFailure) return desired.Error;
            var desiredIds = validRoleIds.Value.ToHashSet();
            foreach (var existing in companyAssignments.Where(assignment => !desiredIds.Contains(assignment.RoleId)))
                roleManager.Remove(existing);
            var existingIds = companyAssignments.Select(assignment => assignment.RoleId).ToHashSet();
            foreach (var assignment in desired.Value.Where(assignment => !existingIds.Contains(assignment.RoleId)))
                roleManager.Add(assignment);
            memberships.MarkUpdated(membership);
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
    }

    public async Task<Result> ChangeStatusAsync(ChangeCompanyMemberStatusRequest request,
        CancellationToken cancellationToken)
    {
        var company = await companies.FindByIdAsync(request.CompanyId, cancellationToken);
        if (company is null) return CompanyErrors.NotFound;
        if (company.Status != CompanyStatus.Active) return CompanyErrors.Inactive;

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await lastCompanyAdmin.LockAsync(request.CompanyId, ct);
            var currentCompany = await companies.FindByIdAsync(request.CompanyId, ct);
            if (currentCompany is null) return CompanyErrors.NotFound;
            if (currentCompany.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
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
                var current = await roleManager.ListForMemberAsync(member.Id, ct);
                var companyAssignments = current.Where(assignment => assignment.CompanyId == request.CompanyId).ToArray();
                var companyAdmin = await roleManager.GetByNameAsync(SystemRoles.CompanyAdmin, ct);
                if (companyAdmin is not null && companyAssignments.Any(assignment => assignment.RoleId == companyAdmin.Id))
                {
                    var canRemove = await lastCompanyAdmin.CheckAnotherActiveAdminAsync(request.CompanyId, ct);
                    if (canRemove.IsFailure) return canRemove;
                }

                foreach (var assignment in companyAssignments) roleManager.Remove(assignment);
            }

            membership.SetStatus(targetStatus);
            memberships.MarkUpdated(membership);
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
    }
}
