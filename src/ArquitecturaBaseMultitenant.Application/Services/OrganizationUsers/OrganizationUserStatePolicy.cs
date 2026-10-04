using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// El estado de una persona de la organización, como especificación. Filtrar, mostrar y contar usan la misma regla en
/// una sola expresión SQL (<c>OrganizationUserReader.WithState</c>), y un test de paridad las compara.
/// </summary>
internal static class OrganizationUserStatePolicy
{
    public static OrganizationUserState Resolve(MemberStatus memberStatus, UserStatus? userStatus,
        InvitationStatus? invitationStatus, DateTime? invitationExpiresAtUtc, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        if (memberStatus == MemberStatus.Invited && invitationStatus == InvitationStatus.Pending)
        {
            if (invitationExpiresAtUtc is { } expiresAtUtc && nowUtc >= expiresAtUtc)
                return OrganizationUserState.Expired;
            return OrganizationUserState.Invited;
        }

        if (memberStatus == MemberStatus.Active && userStatus == UserStatus.PendingDeletion)
            return OrganizationUserState.PendingDeletion;
        if (memberStatus == MemberStatus.Active && userStatus == UserStatus.Active)
            return OrganizationUserState.Active;
        return OrganizationUserState.Inactive;
    }
}
