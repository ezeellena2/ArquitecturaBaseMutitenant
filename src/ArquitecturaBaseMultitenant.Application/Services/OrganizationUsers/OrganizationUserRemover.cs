using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// Quita al miembro y revoca sus invitaciones y accesos de empresa dentro de la transacción del servicio. Quitar a quien
/// ya entró pide tener todos sus permisos (ADR 0061, <see cref="OrganizationDelegationGuard"/>); quitar a un invitado es
/// revocar su invitación, que no pide nada.
/// </summary>
internal sealed class OrganizationUserRemover(
    IMemberRepository members,
    IMemberReader memberReader,
    ILoginMethodRepository loginMethods,
    OrganizationUserAssignmentWriter assignments,
    OrganizationUserAdministratorGuard administrators,
    OrganizationUserAccessRemover accessCleaner,
    OrganizationMembershipEndNotifier membershipEndNotifier,
    OrganizationDelegationGuard delegation,
    TimeProvider timeProvider)
{
    internal async Task<Result> RemoveAsync(Guid memberId, uint version, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(memberId, version, ct);
        if (member is null) return MemberErrors.NotFound;
        if (member.Status == MemberStatus.Removed) return MemberErrors.InvalidTransition;
        if (member.UserId is { } changingUserId)
            await loginMethods.LockUserAsync(changingUserId, ct);
        var membershipWasActive = member.Status == MemberStatus.Active;
        var user = member.UserId is { } userId ? await memberReader.FindByUserIdAsync(userId, ct) : null;
        var snapshot = await assignments.GetSnapshotAsync(member.Id, ct);
        if (member.Status != MemberStatus.Invited
            && await delegation.CheckActOnAsync(member, snapshot.Assignments, ct) is { IsFailure: true } denied)
            return denied;
        var protectedResult = await administrators.ProtectAsync(member, user?.UserStatus, snapshot,
            desired: null, deactivating: true, removing: true, ct);
        if (protectedResult.IsFailure) return protectedResult;
        await accessCleaner.RemoveMembershipAccessAsync(member.Id, snapshot, timeProvider.GetUtcNow().UtcDateTime, ct);
        var removed = member.Remove();
        if (removed.IsFailure) return removed;
        members.MarkUpdated(member);
        if (member.UserId is { } removedUserId)
        {
            if (membershipWasActive)
            {
                var notice = await membershipEndNotifier.NotifyAsync(removedUserId, ct);
                if (notice.IsFailure) return notice;
            }
            await assignments.NotifyAccessLostAsync(removedUserId, ct);
        }
        return Result.Success();
    }
}
