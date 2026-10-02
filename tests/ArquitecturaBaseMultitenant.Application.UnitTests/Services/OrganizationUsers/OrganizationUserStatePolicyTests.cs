using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.OrganizationUsers;

public sealed class OrganizationUserStatePolicyTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(MemberStatus.Active, UserStatus.Active, null, null, OrganizationUserState.Active)]
    [InlineData(MemberStatus.Active, UserStatus.PendingDeletion, null, null, OrganizationUserState.PendingDeletion)]
    [InlineData(MemberStatus.Inactive, UserStatus.Active, null, null, OrganizationUserState.Inactive)]
    [InlineData(MemberStatus.Invited, null, InvitationStatus.Pending, 1, OrganizationUserState.Invited)]
    [InlineData(MemberStatus.Invited, null, InvitationStatus.Pending, 0, OrganizationUserState.Expired)]
    public void Resolves_the_visible_organization_state(MemberStatus memberStatus, UserStatus? userStatus,
        InvitationStatus? invitationStatus, int? expiresInMinutes, OrganizationUserState expected)
    {
        DateTime? expiresAtUtc = expiresInMinutes is { } minutes ? NowUtc.AddMinutes(minutes) : null;

        Assert.Equal(expected, OrganizationUserStatePolicy.Resolve(memberStatus, userStatus,
            invitationStatus, expiresAtUtc, NowUtc));
    }
}
