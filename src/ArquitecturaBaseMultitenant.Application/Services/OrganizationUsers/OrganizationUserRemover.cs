using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Quita al miembro y revoca sus invitaciones y accesos de empresa dentro de la transacción del servicio.</summary>
internal sealed class OrganizationUserRemover(
    IMemberRepository members,
    IMemberReader memberReader,
    ILoginMethodRepository loginMethods,
    OrganizationUserAssignmentWriter assignments,
    OrganizationUserAdministratorGuard administrators,
    OrganizationUserAccessRemover accessCleaner,
    OrganizationMembershipEndNotifier membershipEndNotifier,
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
        var protectedResult = await administrators.ProtectAsync(member, user?.UserStatus, snapshot,
            desired: null, deactivating: true, removing: true, ct);
        if (protectedResult.IsFailure) return protectedResult;
        await accessCleaner.RemoveMembershipAccessAsync(member.Id, snapshot, timeProvider.GetUtcNow().UtcDateTime, ct);
        var removed = member.Remove();
        if (removed.IsFailure) return removed;
        members.MarkUpdated(member);
        if (membershipWasActive && member.UserId is { } removedUserId)
        {
            var notice = await membershipEndNotifier.NotifyAsync(removedUserId, ct);
            if (notice.IsFailure) return notice;
        }
        return Result.Success();
    }
}
