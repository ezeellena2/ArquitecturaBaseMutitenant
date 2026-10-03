using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Agrupa las notificaciones y limpieza que acompañan la salida de una persona de la organización.</summary>
internal sealed class OrganizationUserExitEffects(
    OrganizationUserAccessCleaner accessCleaner,
    OrganizationMembershipEndNotifier membershipEndNotifier)
{
    internal Task RemoveMembershipAccessAsync(Guid memberId, OrganizationUserAssignmentSnapshot snapshot,
        DateTime nowUtc, CancellationToken cancellationToken) =>
        accessCleaner.RemoveMembershipAccessAsync(memberId, snapshot, nowUtc, cancellationToken);

    internal Task<Result> NotifyMembershipEndedAsync(Guid userId, CancellationToken cancellationToken) =>
        membershipEndNotifier.NotifyAsync(userId, cancellationToken);
}
