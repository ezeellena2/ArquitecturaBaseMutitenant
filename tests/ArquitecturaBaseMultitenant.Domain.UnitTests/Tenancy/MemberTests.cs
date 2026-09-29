using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Tenancy;

public sealed class MemberTests
{
    [Fact]
    public void Member_moves_from_invited_through_active_and_inactive_to_removed()
    {
        var member = Member.Invite(Guid.CreateVersion7());
        var joinedAtUtc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(MemberStatus.Invited, member.Status);
        Assert.True(member.Activate(joinedAtUtc).IsSuccess);
        Assert.Equal(MemberStatus.Active, member.Status);
        Assert.Equal(joinedAtUtc, member.JoinedAtUtc);
        Assert.True(member.Deactivate().IsSuccess);
        Assert.Equal(MemberStatus.Inactive, member.Status);
        Assert.True(member.Activate(joinedAtUtc.AddDays(1)).IsSuccess);
        Assert.Equal(joinedAtUtc, member.JoinedAtUtc);
        Assert.True(member.Remove().IsSuccess);
        Assert.Equal(MemberStatus.Removed, member.Status);
        Assert.Equal(MemberErrors.InvalidTransition, member.Activate(joinedAtUtc.AddDays(2)).Error);
    }

    [Fact]
    public void Member_rejects_invalid_transitions()
    {
        var member = Member.Invite(Guid.CreateVersion7());

        Assert.Equal(MemberErrors.InvalidTransition, member.Deactivate().Error);
        Assert.True(member.Remove().IsSuccess);
        Assert.Equal(MemberErrors.InvalidTransition, member.Remove().Error);
    }

    [Fact]
    public void Member_has_no_ownership_flag()
    {
        Assert.Null(typeof(Member).GetProperty("IsOwner"));
        Assert.True(typeof(ITenantOwned).IsAssignableFrom(typeof(Member)));
    }

    [Fact]
    public void Tenant_column_starts_unset_for_the_interceptor_to_stamp()
    {
        var member = Member.Invite(Guid.CreateVersion7());

        Assert.Equal(Guid.Empty, member.TenantId);
    }
}
