using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Quita al miembro y revoca invitaciones y accesos de empresa dentro de una sola transacción.</summary>
internal sealed class OrganizationUserRemover(
    IMemberRepository members,
    IMemberReader memberReader,
    ILoginMethodRepository loginMethods,
    OrganizationUserAssignmentManager assignments,
    OrganizationUserAdministratorGuard administrators,
    OrganizationUserExitEffects exitEffects,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    internal Task<Result> RemoveAsync(Guid memberId, uint version, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
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
            await exitEffects.RemoveMembershipAccessAsync(member.Id, snapshot,
                timeProvider.GetUtcNow().UtcDateTime, ct);
            var removed = member.Remove();
            if (removed.IsFailure) return removed;
            members.MarkUpdated(member);
            if (membershipWasActive && member.UserId is { } removedUserId)
            {
                var notice = await exitEffects.NotifyMembershipEndedAsync(removedUserId, ct);
                if (notice.IsFailure) return notice;
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
}
