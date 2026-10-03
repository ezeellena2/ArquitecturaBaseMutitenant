using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Edita roles y membresías después de proteger a los administradores actuales.</summary>
internal sealed class OrganizationUserEditor(
    IMemberRepository members,
    IMemberReader memberReader,
    OrganizationUserAssignmentManager assignments,
    OrganizationUserAdministratorGuard administrators,
    IUnitOfWork unitOfWork)
{
    internal Task<Result> UpdateAsync(UpdateOrganizationUserRequest request,
        DesiredOrganizationUserAssignments desired, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var member = await members.GetByIdAsync(request.MemberId, request.Version!.Value, ct);
            if (member is null) return MemberErrors.NotFound;
            if (member.Status == MemberStatus.Removed) return MemberErrors.InvalidTransition;
            var user = member.UserId is { } userId ? await memberReader.FindByUserIdAsync(userId, ct) : null;
            if (user?.UserStatus == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
            var snapshot = await assignments.GetSnapshotAsync(member.Id, ct);
            var protectedResult = await administrators.ProtectAsync(member, user?.UserStatus, snapshot,
                desired, deactivating: false, removing: false, ct);
            if (protectedResult.IsFailure) return protectedResult;
            var changed = await assignments.SetAsync(member.Id, desired, ct, snapshot);
            if (changed.IsFailure) return changed;
            members.MarkUpdated(member);
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
}
