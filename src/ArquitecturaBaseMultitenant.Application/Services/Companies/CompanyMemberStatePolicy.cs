using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>
/// El estado de un miembro de empresa, como especificación. Filtrar, mostrar y contar usan la misma regla en una sola
/// expresión SQL (<c>CompanyMembershipReader.WithState</c>), y un test de paridad las compara.
/// </summary>
internal static class CompanyMemberStatePolicy
{
    public static CompanyMemberState Resolve(MemberStatus memberStatus, UserStatus? userStatus,
        CompanyMembershipStatus membershipStatus)
    {
        if (memberStatus == MemberStatus.Invited) return CompanyMemberState.Invited;
        if (membershipStatus == CompanyMembershipStatus.Inactive || memberStatus != MemberStatus.Active
            || userStatus is UserStatus.Suspended or UserStatus.Deleted)
            return CompanyMemberState.Inactive;
        return userStatus == UserStatus.PendingDeletion
            ? CompanyMemberState.PendingDeletion : CompanyMemberState.Active;
    }
}
