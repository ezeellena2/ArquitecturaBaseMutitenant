using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// Edita estado, roles y membresías dentro de la transacción del servicio, después de proteger a los administradores
/// actuales. Quien edita tiene todos los permisos de la persona (ADR 0061, <see cref="OrganizationDelegationGuard"/>), y
/// cada rol que entra o sale lo controla <see cref="OrganizationUserAssignmentWriter"/>.
/// </summary>
internal sealed class OrganizationUserEditor(
    IMemberRepository members,
    IMemberReader memberReader,
    OrganizationUserAssignmentWriter assignments,
    OrganizationUserAdministratorGuard administrators,
    OrganizationUserStatusEditor statusEditor,
    OrganizationDelegationGuard delegation)
{
    internal async Task<Result<Member>> UpdateAsync(UpdateOrganizationUserRequest request,
        DesiredOrganizationUserAssignments desired, Guid? currentUserId, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(request.MemberId, request.Version!.Value, ct);
        if (member is null) return MemberErrors.NotFound;
        if (member.Status == MemberStatus.Removed) return MemberErrors.InvalidTransition;
        var user = member.UserId is { } userId ? await memberReader.FindByUserIdAsync(userId, ct) : null;
        if (user?.UserStatus == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
        var snapshot = await assignments.GetSnapshotAsync(member.Id, ct);
        var actedOn = await delegation.CheckActOnAsync(member, snapshot.Assignments, ct);
        if (actedOn.IsFailure) return actedOn.Error;
        if (request.IsActive is { } isActive)
        {
            var status = await statusEditor.ApplyAsync(member, isActive, currentUserId, ct);
            if (status.IsFailure) return status.Error;
        }
        var protectedResult = await administrators.ProtectAsync(member, user?.UserStatus, snapshot,
            desired, deactivating: false, removing: false, ct);
        if (protectedResult.IsFailure) return protectedResult.Error;
        var changed = await assignments.SetAsync(member.Id, desired, ct, snapshot);
        if (changed.IsFailure) return changed.Error;
        members.MarkUpdated(member);
        return member;
    }
}
