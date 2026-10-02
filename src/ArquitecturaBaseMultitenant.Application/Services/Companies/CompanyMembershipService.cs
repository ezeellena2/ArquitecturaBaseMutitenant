using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Administra los miembros y roles de una empresa sin alterar sus membresías en otras empresas.</summary>
internal sealed class CompanyMembershipService(
    ICompanyMembershipReader reader,
    ICompanyReader companies,
    ICompanyMembershipRepository memberships,
    IMemberRepository members,
    IMemberReader memberReader,
    IRoleRepository roles,
    IRoleReader roleReader,
    IRoleAssignmentRepository assignments,
    ILastCompanyAdminGuard lastCompanyAdmin,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CompanyMembershipService> logger) : ICompanyMembershipService
{
    public Task<Result<PagedResult<CompanyMemberResponse>>> ListAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListCompanyMembers", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var company = await companies.FindByIdAsync(companyId, cancellationToken);
            if (company is null) return CompanyErrors.NotFound;
            var page = await reader.ListAsync(companyId, request, cancellationToken);
            return Result.Success(new PagedResult<CompanyMemberResponse>([.. page.Items.Select(ToResponse)],
                page.Page, page.PageSize, page.TotalCount));
        });
    }

    public Task<Result<CompanyMemberFilterCountsResponse>> GetFilterCountsAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "GetCompanyMemberFilterCounts", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await companies.FindByIdAsync(companyId, cancellationToken) is null) return CompanyErrors.NotFound;
            return Result.Success(await reader.GetFilterCountsAsync(companyId, request, cancellationToken));
        });
    }

    public Task<Result> AddAsync(AddCompanyMemberRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "AddCompanyMember", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var company = await companies.FindByIdAsync(request.CompanyId, cancellationToken);
            if (company is null) return CompanyErrors.NotFound;
            if (company.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
            var validRoleIds = await ValidateRoleIdsAsync(request.CompanyId, request.RoleIds!.ToArray(), cancellationToken);
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

                var assignmentsToAdd = await CreateAssignmentsAsync(member.Id, request.CompanyId,
                    validRoleIds.Value, ct);
                if (assignmentsToAdd.IsFailure) return assignmentsToAdd.Error;
                var membership = CompanyMembership.Create(request.CompanyId, member.Id);
                if (membership.IsFailure) return membership.Error;
                memberships.Add(membership.Value);
                foreach (var assignment in assignmentsToAdd.Value) assignments.Add(assignment);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    public Task<Result> UpdateRolesAsync(UpdateCompanyMemberRolesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "UpdateCompanyMemberRoles", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var company = await companies.FindByIdAsync(request.CompanyId, cancellationToken);
            if (company is null) return CompanyErrors.NotFound;
            if (company.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
            var validRoleIds = await ValidateRoleIdsAsync(request.CompanyId, request.RoleIds!.ToArray(), cancellationToken);
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

                var current = await assignments.ListForMemberAsync(member.Id, ct);
                var companyAssignments = current.Where(assignment => assignment.CompanyId == request.CompanyId).ToArray();
                var companyAdmin = await roles.GetByNameAsync(SystemRoles.CompanyAdmin, ct);
                var removesLastAdmin = companyAdmin is not null
                    && companyAssignments.Any(assignment => assignment.RoleId == companyAdmin.Id)
                    && !validRoleIds.Value.Contains(companyAdmin.Id);
                if (removesLastAdmin)
                {
                    var canRemove = await lastCompanyAdmin.CheckAnotherActiveAdminAsync(request.CompanyId, ct);
                    if (canRemove.IsFailure) return canRemove;
                }

                var desired = await CreateAssignmentsAsync(member.Id, request.CompanyId, validRoleIds.Value, ct);
                if (desired.IsFailure) return desired.Error;
                var desiredIds = validRoleIds.Value.ToHashSet();
                foreach (var existing in companyAssignments.Where(assignment => !desiredIds.Contains(assignment.RoleId)))
                    assignments.Remove(existing);
                var existingIds = companyAssignments.Select(assignment => assignment.RoleId).ToHashSet();
                foreach (var assignment in desired.Value.Where(assignment => !existingIds.Contains(assignment.RoleId)))
                    assignments.Add(assignment);
                memberships.MarkUpdated(membership);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    public Task<Result> ChangeStatusAsync(ChangeCompanyMemberStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ChangeCompanyMemberStatus", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
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
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    private async Task<Result<Guid[]>> ValidateRoleIdsAsync(Guid companyId, Guid[] roleIds,
        CancellationToken cancellationToken)
    {
        var normalized = roleIds.Distinct().ToArray();
        foreach (var roleId in normalized)
        {
            var role = await roleReader.FindByIdAsync(roleId, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope == RoleScope.Organization) return RoleErrors.CompanyNotAllowed;
            if (role.Scope == RoleScope.SpecificCompany && role.CompanyId != companyId)
                return RoleAssignmentErrors.CompanyMismatch;
        }

        return normalized;
    }

    private async Task<Result<RoleAssignment[]>> CreateAssignmentsAsync(Guid memberId, Guid companyId,
        Guid[] roleIds, CancellationToken cancellationToken)
    {
        var result = new List<RoleAssignment>(roleIds.Length);
        foreach (var roleId in roleIds)
        {
            var row = await roleReader.FindByIdAsync(roleId, cancellationToken);
            if (row is null) return RoleErrors.NotFound;
            var role = await roles.GetByIdAsync(roleId, row.Version, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope == RoleScope.Organization) return RoleErrors.CompanyNotAllowed;
            if (role.Scope == RoleScope.SpecificCompany && role.CompanyId != companyId)
                return RoleAssignmentErrors.CompanyMismatch;
            var assignment = RoleAssignment.Assign(memberId, role, companyId);
            if (assignment.IsFailure) return assignment.Error;
            result.Add(assignment.Value);
        }

        return result.ToArray();
    }

    private static CompanyMemberResponse ToResponse(CompanyMemberRow member) => new(member.MemberId, member.UserId,
        member.DisplayName, member.Email, GetState(member), member.MembershipStatus,
        [.. member.Roles.Select(role => new CompanyMemberRoleResponse(role.Id, RoleTexts.Name(role.Name),
            role.IsSystemRole, role.Scope, role.CompanyId))], member.Version, member.CreatedAtUtc);

    private static CompanyMemberState GetState(CompanyMemberRow member)
    {
        if (member.MemberStatus == MemberStatus.Invited) return CompanyMemberState.Invited;
        if (member.MembershipStatus == CompanyMembershipStatus.Inactive || member.MemberStatus != MemberStatus.Active
            || member.UserStatus is UserStatus.Suspended or UserStatus.Deleted)
            return CompanyMemberState.Inactive;
        return member.UserStatus == UserStatus.PendingDeletion
            ? CompanyMemberState.PendingDeletion : CompanyMemberState.Active;
    }
}
