using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Coordina invitaciones de organización y mantiene cada reenvío como invitación independiente.</summary>
internal sealed class OrganizationUserInvitationManager(
    IInvitationRepository invitations,
    InvitationIssuer issuer,
    IExternalLoginLock externalLoginLock,
    ICurrentUser currentUser,
    OrganizationUserAccessCleaner accessCleaner,
    OrganizationUserAssignmentManager assignmentManager,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    internal async Task<Result<Guid>> InviteAsync(InviteOrganizationUserRequest request,
        DesiredOrganizationUserAssignments desired, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } inviterId || inviterId == Guid.Empty) return AccessErrors.NotMember;
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var issued = await issuer.IssueWithMemberAsync(
                new IssueInvitationRequest(request.Email!, inviterId, InvitationChannel.Email), ct, grant: Grant(desired));
            if (issued.IsFailure) return issued.Error;
            var assigned = await assignmentManager.SetAsync(issued.Value.MemberId, desired, ct,
                memberInUnitOfWork: issued.Value.Member);
            return assigned.IsFailure ? assigned.Error : Result<Guid>.Success(issued.Value.InvitationId);
        }, CommitPolicy.OnSuccess, cancellationToken);
    }

    // Lo que el correo muestra como Empresa y Roles, en el mismo orden que la vista previa.
    private static InvitationGrantRow Grant(DesiredOrganizationUserAssignments desired) => new([],
        [.. desired.CompanyRoles.Values.Select(selection => selection.Company.Name).Order(StringComparer.CurrentCulture)],
        [.. desired.OrganizationRoles.Select(role => role.Name).Order(StringComparer.Ordinal)
            .Concat(desired.CompanyRoles.Values.SelectMany(selection => selection.Roles).Select(role => role.Name)
                .Order(StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)]);

    internal Task<Result<Guid>> ResendAsync(Guid invitationId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var firstRead = await invitations.GetByIdAsync(invitationId, ct);
            if (firstRead is null) return InvitationErrors.Invalid;
            await externalLoginLock.LockEmailAsync(firstRead.Destination, ct);
            await invitations.LockDestinationAsync(firstRead.Destination, ct);
            await invitations.LockAsync(invitationId, ct);
            var invitation = await invitations.GetByIdAsync(invitationId, ct);
            if (invitation is null || invitation.Status != InvitationStatus.Pending)
                return InvitationErrors.Invalid;
            var revoked = invitation.Revoke(timeProvider.GetUtcNow().UtcDateTime);
            if (revoked.IsFailure) return revoked.Error;
            if (currentUser.UserId is not { } inviterId || inviterId == Guid.Empty) return AccessErrors.NotMember;
            return await issuer.IssueAsync(new IssueInvitationRequest(invitation.Destination, inviterId,
                invitation.Channel, invitation.MemberId, invitation.Id), ct);
        }, CommitPolicy.OnSuccess, cancellationToken);

    internal Task<Result> RevokeAsync(Guid invitationId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
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
            return await accessCleaner.RemoveUnacceptedMemberIfOrphanedAsync(invitationId,
                invitation.MemberId, ct);
        }, CommitPolicy.OnSuccess, cancellationToken);
}
