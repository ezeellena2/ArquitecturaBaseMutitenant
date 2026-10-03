using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Limpia accesos y membresías de empresa al revocar una invitación huérfana o quitar a alguien.</summary>
internal sealed class OrganizationUserAccessCleaner(
    IInvitationRepository invitations,
    IMemberRepository members,
    OrganizationUserAssignmentManager assignments)
{
    internal async Task<Result> RemoveUnacceptedMemberIfOrphanedAsync(Guid invitationId, Guid memberId,
        CancellationToken cancellationToken)
    {
        var member = await members.GetByIdAsync(memberId, cancellationToken);
        if (member is not { Status: MemberStatus.Invited, UserId: null }) return Result.Success();
        var pendingInvitations = await invitations.ListPendingForMemberAsync(member.Id, cancellationToken);
        if (pendingInvitations.Any(pending => pending.Id != invitationId
            && pending.Status == InvitationStatus.Pending))
            return Result.Success();

        var removed = member.Remove();
        if (removed.IsFailure) return removed;
        members.MarkUpdated(member);
        assignments.Clear(await assignments.GetSnapshotAsync(member.Id, cancellationToken));
        return Result.Success();
    }

    internal async Task RemoveMembershipAccessAsync(Guid memberId, OrganizationUserAssignmentSnapshot snapshot,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        foreach (var pending in await invitations.ListPendingForMemberAsync(memberId, cancellationToken))
            pending.Revoke(nowUtc);
        assignments.Clear(snapshot);
    }
}
