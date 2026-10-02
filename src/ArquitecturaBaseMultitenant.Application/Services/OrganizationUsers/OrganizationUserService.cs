using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Administra miembros e invitaciones sin alterar la cuenta global ni otras organizaciones.</summary>
internal sealed class OrganizationUserService(IOrganizationUserReader reader, IMemberRepository members,
    IMemberReader memberReader, ICompanyMembershipRepository companyMemberships,
    IRoleAssignmentRepository assignments, IInvitationRepository invitations, InvitationIssuer issuer,
    IRoleReader roleReader, IRoleRepository roles, ICompanyReader companies,
    ILastTenantAdminGuard lastTenantAdmin, ILastCompanyAdminGuard lastCompanyAdmin,
    ILoginMethodRepository loginMethods, ILoginMethodMembershipReader loginMethodMemberships,
    IUserRepository users, ITenantReader tenants, ISecurityEventRepository securityEvents,
    AccountNoticeIssuer accountNotices, ITenantContext tenantContext, IPublicOrigin publicOrigin,
    IExternalLoginLock externalLoginLock, ICurrentUser currentUser, IRequestValidator validator,
    IUnitOfWork unitOfWork, TimeProvider timeProvider, ILogger<OrganizationUserService> logger)
    : IOrganizationUserService
{
    public Task<Result<PagedResult<OrganizationUserResponse>>> ListAsync(ListOrganizationUsersRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListOrganizationUsers", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await reader.ListAsync(request, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            return Result.Success(new PagedResult<OrganizationUserResponse>([.. page.Items.Select(ToResponse)],
                page.Page, page.PageSize, page.TotalCount));
        });
    }

    public Task<Result<OrganizationUserFilterCountsResponse>> GetFilterCountsAsync(
        ListOrganizationUsersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "GetOrganizationUserFilterCounts", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return Result.Success(await reader.GetFilterCountsAsync(request,
                timeProvider.GetUtcNow().UtcDateTime, cancellationToken));
        });
    }

    public Task<Result<OrganizationUserResponse>> GetAsync(Guid memberId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<OrganizationUserResponse>(logger, timeProvider, "GetOrganizationUser", async () =>
        {
            var row = await reader.FindByIdAsync(memberId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            return row is null ? MemberErrors.NotFound : ToResponse(row);
        });

    public Task<Result<Guid>> InviteAsync(InviteOrganizationUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "InviteOrganizationUser", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var email = Email.Create(request.Email);
            if (email.IsFailure) return email.Error;
            if (currentUser.UserId is not { } inviterId || inviterId == Guid.Empty) return AccessErrors.NotMember;
            var desired = await ValidateAssignmentsAsync(request.RoleIds!, request.Companies!, cancellationToken);
            if (desired.IsFailure) return desired.Error;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var invitationId = await issuer.IssueAsync(
                    new IssueInvitationRequest(email.Value, inviterId, InvitationChannel.Email), ct);
                if (invitationId.IsFailure) return invitationId.Error;
                var invitation = await invitations.GetByIdAsync(invitationId.Value, ct)
                    ?? throw new InvalidOperationException("The issuer did not persist its invitation in the caller's unit of work.");
                var assigned = await SetAssignmentsAsync(invitation.MemberId, desired.Value, ct);
                return assigned.IsFailure ? assigned.Error : invitationId;
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    public Task<Result<Guid>> ResendInvitationAsync(Guid invitationId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "ResendOrganizationInvitation", async () =>
        {
            if (invitationId == Guid.Empty) return InvitationErrors.Invalid;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var firstRead = await invitations.GetByIdAsync(invitationId, ct);
                if (firstRead is null) return InvitationErrors.Invalid;
                await externalLoginLock.LockEmailAsync(firstRead.Destination, ct);
                await invitations.LockDestinationAsync(firstRead.Destination, ct);
                await invitations.LockAsync(invitationId, ct);
                var invitation = await invitations.GetByIdAsync(invitationId, ct);
                if (invitation is null) return InvitationErrors.Invalid;
                if (invitation.Status != InvitationStatus.Pending) return InvitationErrors.Invalid;
                var revoked = invitation.Revoke(timeProvider.GetUtcNow().UtcDateTime);
                if (revoked.IsFailure) return revoked.Error;
                if (currentUser.UserId is not { } inviterId || inviterId == Guid.Empty) return AccessErrors.NotMember;
                return await issuer.IssueAsync(new IssueInvitationRequest(invitation.Destination, inviterId,
                    invitation.Channel, invitation.MemberId), ct);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "RevokeOrganizationInvitation", async () =>
        {
            if (invitationId == Guid.Empty) return InvitationErrors.Invalid;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var firstRead = await invitations.GetByIdAsync(invitationId, ct);
                if (firstRead is null) return InvitationErrors.Invalid;
                await externalLoginLock.LockEmailAsync(firstRead.Destination, ct);
                await invitations.LockDestinationAsync(firstRead.Destination, ct);
                await invitations.LockAsync(invitationId, ct);
                var invitation = await invitations.GetByIdAsync(invitationId, ct);
                if (invitation is null) return InvitationErrors.Invalid;
                var revoked = invitation.Revoke(timeProvider.GetUtcNow().UtcDateTime);
                if (revoked.IsFailure) return revoked;

                var member = await members.GetByIdAsync(invitation.MemberId, ct);
                if (member is not { Status: MemberStatus.Invited, UserId: null }) return Result.Success();
                var otherPending = await invitations.ListPendingForMemberAsync(member.Id, ct);
                if (otherPending.Any(pending => pending.Id != invitationId && pending.Status == InvitationStatus.Pending))
                    return Result.Success();

                var removed = member.Remove();
                if (removed.IsFailure) return removed;
                members.MarkUpdated(member);
                foreach (var assignment in await assignments.ListForMemberAsync(member.Id, ct))
                    assignments.Remove(assignment);
                foreach (var membership in await companyMemberships.ListForMemberAsync(member.Id, ct))
                {
                    if (membership.Status != CompanyMembershipStatus.Active) continue;
                    membership.SetStatus(CompanyMembershipStatus.Inactive);
                    companyMemberships.MarkUpdated(membership);
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> UpdateAsync(UpdateOrganizationUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "UpdateOrganizationUser", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var desired = await ValidateAssignmentsAsync(request.RoleIds!, request.Companies!, cancellationToken);
            if (desired.IsFailure) return desired.Error;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var member = await members.GetByIdAsync(request.MemberId, request.Version!.Value, ct);
                if (member is null) return MemberErrors.NotFound;
                if (member.Status is MemberStatus.Removed) return MemberErrors.InvalidTransition;
                var user = member.UserId is { } userId ? await memberReader.FindByUserIdAsync(userId, ct) : null;
                if (user?.UserStatus == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
                var currentAssignments = await assignments.ListForMemberAsync(member.Id, ct);
                var currentMemberships = await companyMemberships.ListForMemberAsync(member.Id, ct);
                var protectedResult = await ProtectAdministratorChangesAsync(member, user?.UserStatus,
                    currentAssignments, currentMemberships, desired.Value, deactivating: false, removing: false, ct);
                if (protectedResult.IsFailure) return protectedResult;
                var changed = await SetAssignmentsAsync(member.Id, desired.Value, ct, currentAssignments,
                    currentMemberships);
                if (changed.IsFailure) return changed;
                members.MarkUpdated(member);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    public Task<Result> ChangeStatusAsync(ChangeOrganizationUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ChangeOrganizationUserStatus", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var member = await members.GetByIdAsync(request.MemberId, request.Version!.Value, ct);
                if (member is null) return MemberErrors.NotFound;
                if (member.Status is MemberStatus.Invited or MemberStatus.Removed)
                    return MemberErrors.InvalidTransition;
                if (member.Status == MemberStatus.Active && request.IsActive == false
                    && currentUser.UserId == member.UserId)
                    return UserErrors.CannotModifySelf;
                if (request.IsActive == (member.Status == MemberStatus.Active)) return Result.Success();

                if (member.UserId is { } changingUserId)
                    await loginMethods.LockUserAsync(changingUserId, ct);
                var user = member.UserId is { } userId ? await memberReader.FindByUserIdAsync(userId, ct) : null;
                var currentAssignments = await assignments.ListForMemberAsync(member.Id, ct);
                var currentMemberships = await companyMemberships.ListForMemberAsync(member.Id, ct);
                if (request.IsActive == true)
                {
                    if (user?.UserStatus == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
                    if (user?.UserStatus != UserStatus.Active) return AccountErrors.Suspended;
                    var activated = member.Activate(timeProvider.GetUtcNow().UtcDateTime);
                    if (activated.IsFailure) return activated;
                }
                else
                {
                    var protectedResult = await ProtectAdministratorChangesAsync(member, user?.UserStatus,
                        currentAssignments, currentMemberships, desired: null, deactivating: true, removing: false, ct);
                    if (protectedResult.IsFailure) return protectedResult;
                    var deactivated = member.Deactivate();
                    if (deactivated.IsFailure) return deactivated;
                    if (member.UserId is { } deactivatedUserId)
                    {
                        var notice = await NotifyMembershipEndedAsync(deactivatedUserId, ct);
                        if (notice.IsFailure) return notice;
                    }
                }
                members.MarkUpdated(member);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    public Task<Result> RemoveAsync(Guid memberId, uint? version, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "RemoveOrganizationUser", async () =>
        {
            if (memberId == Guid.Empty || version is null) return UserErrors.NotFound;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var member = await members.GetByIdAsync(memberId, version.Value, ct);
                if (member is null) return MemberErrors.NotFound;
                if (member.Status == MemberStatus.Removed) return MemberErrors.InvalidTransition;
                if (member.UserId is { } changingUserId)
                    await loginMethods.LockUserAsync(changingUserId, ct);
                var membershipWasActive = member.Status == MemberStatus.Active;
                var user = member.UserId is { } userId ? await memberReader.FindByUserIdAsync(userId, ct) : null;
                var currentAssignments = await assignments.ListForMemberAsync(member.Id, ct);
                var currentMemberships = await companyMemberships.ListForMemberAsync(member.Id, ct);
                var protectedResult = await ProtectAdministratorChangesAsync(member, user?.UserStatus,
                    currentAssignments, currentMemberships, desired: null, deactivating: true, removing: true, ct);
                if (protectedResult.IsFailure) return protectedResult;
                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                foreach (var pending in await invitations.ListPendingForMemberAsync(member.Id, ct))
                    pending.Revoke(nowUtc);
                foreach (var assignment in currentAssignments) assignments.Remove(assignment);
                foreach (var membership in currentMemberships.Where(row => row.Status == CompanyMembershipStatus.Active))
                {
                    membership.SetStatus(CompanyMembershipStatus.Inactive);
                    companyMemberships.MarkUpdated(membership);
                }
                var removed = member.Remove();
                if (removed.IsFailure) return removed;
                members.MarkUpdated(member);
                if (membershipWasActive && member.UserId is { } removedUserId)
                {
                    var notice = await NotifyMembershipEndedAsync(removedUserId, ct);
                    if (notice.IsFailure) return notice;
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    private async Task<Result> NotifyMembershipEndedAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } actorId || actorId == Guid.Empty) return AccessErrors.NotMember;
        var tenantId = tenantContext.RequiredTenantId;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        securityEvents.Add(SecurityEvent.ForOrganizationMembershipEnded(actorId, tenantId, nowUtc));

        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status == UserStatus.Deleted) return Result.Success();
        var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null) return MemberErrors.NotFound;

        var availableMethods = new List<LoginMethod>();
        foreach (var method in await loginMethods.ListByUserIdAsync(userId, cancellationToken))
        {
            if (method.ManagedByTenantId == tenantId || method.InvitationOriginTenantId == tenantId) continue;
            if (method.ManagedByTenantId is { } ownerTenantId
                && !await loginMethodMemberships.IsActiveAsync(userId, ownerTenantId, cancellationToken))
                continue;
            availableMethods.Add(method);
        }

        var accountUrl = publicOrigin.Value is { } origin ? new Uri(origin, "/cuenta").AbsoluteUri : "/cuenta";
        await accountNotices.EnqueueAsync(userId, availableMethods,
            new AccountNotice.ReviewLoginMethods(tenant.Name, accountUrl) { RecipientName = account.DisplayName },
            account.Culture, principalOnly: false, cancellationToken);
        return Result.Success();
    }

    private async Task<Result<DesiredAssignments>> ValidateAssignmentsAsync(IReadOnlyCollection<Guid> roleIds,
        IReadOnlyCollection<InviteCompanyRolesRequest> requestedCompanies, CancellationToken cancellationToken)
    {
        var organizationRoles = new List<RoleRow>();
        foreach (var id in roleIds.Distinct())
        {
            var role = await roleReader.FindByIdAsync(id, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope != RoleScope.Organization) return RoleErrors.CompanyNotAllowed;
            organizationRoles.Add(role);
        }

        var companyRoles = new Dictionary<Guid, (CompanyRow Company, IReadOnlyList<RoleRow> Roles)>();
        foreach (var requested in requestedCompanies)
        {
            var company = await companies.FindByIdAsync(requested.CompanyId, cancellationToken);
            if (company is null) return CompanyErrors.NotFound;
            if (company.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
            var selected = new List<RoleRow>();
            foreach (var roleId in requested.RoleIds!.Distinct())
            {
                var role = await roleReader.FindByIdAsync(roleId, cancellationToken);
                if (role is null) return RoleErrors.NotFound;
                if (role.Scope == RoleScope.Organization) return RoleAssignmentErrors.CompanyNotAllowed;
                if (role.Scope == RoleScope.SpecificCompany && role.CompanyId != company.Id)
                    return RoleAssignmentErrors.CompanyMismatch;
                selected.Add(role);
            }
            companyRoles.Add(company.Id, (company, selected));
        }

        return new DesiredAssignments(organizationRoles, companyRoles);
    }

    private async Task<Result> SetAssignmentsAsync(Guid memberId, DesiredAssignments desired,
        CancellationToken cancellationToken, IReadOnlyList<RoleAssignment>? existingAssignments = null,
        IReadOnlyList<CompanyMembership>? existingMemberships = null)
    {
        var member = await members.GetByIdAsync(memberId, cancellationToken);
        if (member is null) return MemberErrors.NotFound;
        existingAssignments ??= await assignments.ListForMemberAsync(memberId, cancellationToken);
        existingMemberships ??= await companyMemberships.ListForMemberAsync(memberId, cancellationToken);

        var desiredAssignments = new List<RoleAssignment>();
        foreach (var roleRow in desired.OrganizationRoles)
        {
            var role = await roles.GetByIdAsync(roleRow.Id, roleRow.Version, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope != RoleScope.Organization) return RoleErrors.CompanyNotAllowed;
            var assignment = RoleAssignment.Assign(memberId, role, companyId: null);
            if (assignment.IsFailure) return assignment.Error;
            desiredAssignments.Add(assignment.Value);
        }
        foreach (var (companyId, selection) in desired.CompanyRoles)
        foreach (var roleRow in selection.Roles)
        {
            var role = await roles.GetByIdAsync(roleRow.Id, roleRow.Version, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope == RoleScope.Organization) return RoleAssignmentErrors.CompanyNotAllowed;
            if (role.Scope == RoleScope.SpecificCompany && role.CompanyId != companyId)
                return RoleAssignmentErrors.CompanyMismatch;
            var assignment = RoleAssignment.Assign(memberId, role, companyId);
            if (assignment.IsFailure) return assignment.Error;
            desiredAssignments.Add(assignment.Value);
        }

        var desiredKeys = desiredAssignments.Select(AssignmentKey).ToHashSet();
        var existingKeys = existingAssignments.Select(AssignmentKey).ToHashSet();
        foreach (var assignment in existingAssignments.Where(row => !desiredKeys.Contains(AssignmentKey(row))))
            assignments.Remove(assignment);
        foreach (var assignment in desiredAssignments.Where(row => !existingKeys.Contains(AssignmentKey(row))))
            assignments.Add(assignment);

        var membershipsByCompany = existingMemberships.ToDictionary(row => row.CompanyId);
        var desiredCompanyIds = desired.CompanyRoles.Keys.ToHashSet();
        foreach (var companyId in desiredCompanyIds)
        {
            if (membershipsByCompany.TryGetValue(companyId, out var membership))
            {
                if (membership.Status != CompanyMembershipStatus.Active)
                {
                    membership.SetStatus(CompanyMembershipStatus.Active);
                    companyMemberships.MarkUpdated(membership);
                }
            }
            else
            {
                var created = CompanyMembership.Create(companyId, memberId);
                if (created.IsFailure) return created.Error;
                companyMemberships.Add(created.Value);
            }
        }
        foreach (var membership in existingMemberships.Where(row => row.Status == CompanyMembershipStatus.Active
                     && !desiredCompanyIds.Contains(row.CompanyId)))
        {
            membership.SetStatus(CompanyMembershipStatus.Inactive);
            companyMemberships.MarkUpdated(membership);
        }
        return Result.Success();
    }

    private async Task<Result> ProtectAdministratorChangesAsync(Member member, UserStatus? userStatus,
        IReadOnlyList<RoleAssignment> existingAssignments,
        IReadOnlyList<CompanyMembership> existingMemberships, DesiredAssignments? desired,
        bool deactivating, bool removing, CancellationToken cancellationToken)
    {
        var userIsActive = userStatus == UserStatus.Active;
        var memberIsActive = member.Status == MemberStatus.Active && userIsActive;
        var ownerRole = await roles.GetByNameAsync(SystemRoles.TenantAdmin, cancellationToken);
        var keepsOwner = !deactivating && !removing && desired is not null && ownerRole is not null
            && desired.OrganizationRoles.Any(row => row.Id == ownerRole.Id);
        if (memberIsActive && ownerRole is not null
            && existingAssignments.Any(row => row.RoleId == ownerRole.Id && row.CompanyId is null)
            && !keepsOwner && member.UserId is { } ownerId)
        {
            var canLeave = await lastTenantAdmin.CheckCanLeaveAsync(ownerId, cancellationToken);
            if (canLeave.IsFailure) return canLeave;
        }

        var companyAdmin = await roles.GetByNameAsync(SystemRoles.CompanyAdmin, cancellationToken);
        if (companyAdmin is null) return Result.Success();
        var oldAdminCompanyIds = existingAssignments.Where(row => row.RoleId == companyAdmin.Id
                && row.CompanyId is not null).Select(row => row.CompanyId!.Value).Distinct().Order().ToArray();
        var newAdminCompanyIds = desired is null ? [] : desired.CompanyRoles
            .Where(pair => pair.Value.Roles.Any(role => role.Id == companyAdmin.Id))
            .Select(pair => pair.Key).Order().ToArray();
        foreach (var companyId in oldAdminCompanyIds.Union(newAdminCompanyIds).Distinct().Order())
            await lastCompanyAdmin.LockAsync(companyId, cancellationToken);

        if (!memberIsActive) return Result.Success();
        var membershipByCompany = existingMemberships.ToDictionary(row => row.CompanyId);
        foreach (var companyId in oldAdminCompanyIds)
        {
            var companyMembership = membershipByCompany.GetValueOrDefault(companyId);
            if (companyMembership?.Status != CompanyMembershipStatus.Active) continue;
            if (!deactivating && !removing && newAdminCompanyIds.Contains(companyId)) continue;
            var company = await companies.FindByIdAsync(companyId, cancellationToken);
            if (company?.Status != CompanyStatus.Active) continue;
            var canLeave = await lastCompanyAdmin.CheckAnotherActiveAdminAsync(companyId, cancellationToken);
            if (canLeave.IsFailure) return canLeave;
        }
        return Result.Success();
    }

    private static (Guid RoleId, Guid? CompanyId) AssignmentKey(RoleAssignment assignment) =>
        (assignment.RoleId, assignment.CompanyId);

    private OrganizationUserResponse ToResponse(OrganizationUserRow row) => new(row.MemberId, row.UserId,
        row.DisplayName.Length == 0 ? row.Email ?? string.Empty : row.DisplayName, row.Email,
        OrganizationUserStatePolicy.Resolve(row.MemberStatus, row.UserStatus, row.InvitationStatus,
            row.InvitationExpiresAtUtc, timeProvider.GetUtcNow().UtcDateTime), row.HasManagedEmail,
        [.. row.Roles.Select(Translate)],
        [.. row.Companies.Select(company => company with { Roles = [.. company.Roles.Select(Translate)] })],
        row.InvitationId, row.InvitationIssuedAtUtc, row.InvitationExpiresAtUtc, row.Version, row.CreatedAtUtc);

    private static OrganizationUserRoleRow Translate(OrganizationUserRoleRow role) => role with
    {
        Name = RoleTexts.Name(role.Name),
    };

    private sealed record DesiredAssignments(IReadOnlyList<RoleRow> OrganizationRoles,
        IReadOnlyDictionary<Guid, (CompanyRow Company, IReadOnlyList<RoleRow> Roles)> CompanyRoles);
}
