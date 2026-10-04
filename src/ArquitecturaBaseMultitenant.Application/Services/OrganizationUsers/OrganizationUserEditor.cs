using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// Edita estado, roles y membresías dentro de la transacción del servicio, después de proteger a los administradores
/// actuales.
/// </summary>
internal sealed class OrganizationUserEditor(
    IMemberRepository members,
    IMemberReader memberReader,
    OrganizationUserAssignmentManager assignments,
    OrganizationUserAdministratorGuard administrators,
    OrganizationUserStatusEditor statusEditor)
{
    internal async Task<Result> UpdateAsync(UpdateOrganizationUserRequest request,
        DesiredOrganizationUserAssignments desired, Guid? currentUserId, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(request.MemberId, request.Version!.Value, ct);
        if (member is null) return MemberErrors.NotFound;
        if (member.Status == MemberStatus.Removed) return MemberErrors.InvalidTransition;
        var user = member.UserId is { } userId ? await memberReader.FindByUserIdAsync(userId, ct) : null;
        if (user?.UserStatus == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
        if (request.IsActive is { } isActive)
        {
            var status = await statusEditor.ApplyAsync(member, isActive, currentUserId, ct);
            if (status.IsFailure) return status;
        }
        var snapshot = await assignments.GetSnapshotAsync(member.Id, ct);
        var protectedResult = await administrators.ProtectAsync(member, user?.UserStatus, snapshot,
            desired, deactivating: false, removing: false, ct);
        if (protectedResult.IsFailure) return protectedResult;
        var changed = await assignments.SetAsync(member.Id, desired, ct, snapshot);
        if (changed.IsFailure) return changed;
        members.MarkUpdated(member);
        return Result.Success();
    }
}
