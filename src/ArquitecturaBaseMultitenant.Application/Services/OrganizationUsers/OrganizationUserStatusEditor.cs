using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Activa o desactiva miembros respetando bajas pendientes y la protección del último administrador.</summary>
internal sealed class OrganizationUserStatusEditor(
    IMemberRepository members,
    IMemberReader memberReader,
    ILoginMethodRepository loginMethods,
    OrganizationUserAssignmentManager assignments,
    OrganizationUserAdministratorGuard administrators,
    OrganizationMembershipEndNotifier membershipEndNotifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    internal Task<Result> ChangeStatusAsync(ChangeOrganizationUserStatusRequest request, Guid? currentUserId,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var member = await members.GetByIdAsync(request.MemberId, request.Version!.Value, ct);
            if (member is null) return MemberErrors.NotFound;
            return await ApplyAsync(member, request.IsActive!.Value, currentUserId, ct);
        }, CommitPolicy.OnSuccess, cancellationToken);

    /// <summary>Cambia el estado dentro de una transacción ya abierta; también lo usa «Guardar cambios» del usuario.</summary>
    internal async Task<Result> ApplyAsync(Member member, bool isActive, Guid? currentUserId, CancellationToken ct)
    {
        if (member.Status is MemberStatus.Invited or MemberStatus.Removed)
            return MemberErrors.InvalidTransition;
        if (member.Status == MemberStatus.Active && !isActive && currentUserId == member.UserId)
            return MemberErrors.CannotDisableSelf;
        if (isActive == (member.Status == MemberStatus.Active)) return Result.Success();

        if (member.UserId is { } changingUserId)
            await loginMethods.LockUserAsync(changingUserId, ct);
        var user = member.UserId is { } userId ? await memberReader.FindByUserIdAsync(userId, ct) : null;
        var snapshot = await assignments.GetSnapshotAsync(member.Id, ct);
        if (isActive)
        {
            if (user?.UserStatus == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
            if (user?.UserStatus != UserStatus.Active) return AccountErrors.Suspended;
            var activated = member.Activate(timeProvider.GetUtcNow().UtcDateTime);
            if (activated.IsFailure) return activated;
        }
        else
        {
            var protectedResult = await administrators.ProtectAsync(member, user?.UserStatus, snapshot,
                desired: null, deactivating: true, removing: false, ct);
            if (protectedResult.IsFailure) return protectedResult;
            var deactivated = member.Deactivate();
            if (deactivated.IsFailure) return deactivated;
            if (member.UserId is { } deactivatedUserId)
            {
                var notice = await membershipEndNotifier.NotifyAsync(deactivatedUserId, ct);
                if (notice.IsFailure) return notice;
            }
        }
        members.MarkUpdated(member);
        return Result.Success();
    }
}
