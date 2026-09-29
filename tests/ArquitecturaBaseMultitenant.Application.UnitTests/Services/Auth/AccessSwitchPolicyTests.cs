using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

public sealed class AccessSwitchPolicyTests
{
    [Fact]
    public void Business_without_membership_is_not_member()
    {
        var selected = AccessSwitchPolicy.SelectBusinessTenant([], null, null);

        Assert.Equal(AccessErrors.NotMember.Code, selected.Error.Code);
    }

    [Fact]
    public void The_only_inactive_membership_reports_inactive()
    {
        var member = Member(Guid.CreateVersion7(), MemberStatus.Inactive);

        var selected = AccessSwitchPolicy.SelectBusinessTenant([member], null, null);

        Assert.Equal(MemberErrors.Inactive.Code, selected.Error.Code);
    }

    [Fact]
    public void Active_membership_wins_over_an_inactive_last_business()
    {
        var inactive = Member(Guid.CreateVersion7(), MemberStatus.Inactive);
        var active = Member(Guid.CreateVersion7(), MemberStatus.Active);

        var selected = AccessSwitchPolicy.SelectBusinessTenant([inactive, active], null, inactive.TenantId);

        Assert.True(selected.IsSuccess);
        Assert.Equal(active.TenantId, selected.Value);
    }

    [Fact]
    public void Last_business_is_used_only_when_it_is_active()
    {
        var first = Member(Guid.CreateVersion7(), MemberStatus.Active);
        var last = Member(Guid.CreateVersion7(), MemberStatus.Active);

        var selected = AccessSwitchPolicy.SelectBusinessTenant([first, last], null, last.TenantId);

        Assert.True(selected.IsSuccess);
        Assert.Equal(last.TenantId, selected.Value);
    }

    [Fact]
    public void Sole_active_membership_is_selected_directly()
    {
        var active = Member(Guid.CreateVersion7(), MemberStatus.Active);

        var selected = AccessSwitchPolicy.SelectBusinessTenant([active], null, null);

        Assert.True(selected.IsSuccess);
        Assert.Equal(active.TenantId, selected.Value);
    }

    [Fact]
    public void Explicit_inactive_membership_cannot_be_selected()
    {
        var active = Member(Guid.CreateVersion7(), MemberStatus.Active);
        var inactive = Member(Guid.CreateVersion7(), MemberStatus.Inactive);

        var selected = AccessSwitchPolicy.SelectBusinessTenant([active, inactive], inactive.TenantId, active.TenantId);

        Assert.Equal(MemberErrors.Inactive.Code, selected.Error.Code);
    }

    [Fact]
    public void Explicit_unknown_business_is_not_member()
    {
        var active = Member(Guid.CreateVersion7(), MemberStatus.Active);

        var selected = AccessSwitchPolicy.SelectBusinessTenant([active], Guid.CreateVersion7(), active.TenantId);

        Assert.Equal(AccessErrors.NotMember.Code, selected.Error.Code);
    }

    [Fact]
    public void Without_last_business_selects_oldest_active_membership()
    {
        var recent = Member(Guid.CreateVersion7(), MemberStatus.Active, "Zeta", new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc));
        var oldest = Member(Guid.CreateVersion7(), MemberStatus.Active, "Alfa", new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc));

        var selected = AccessSwitchPolicy.SelectBusinessTenant([recent, oldest], null, null);

        Assert.True(selected.IsSuccess);
        Assert.Equal(oldest.TenantId, selected.Value);
    }

    [Fact]
    public void Equal_join_dates_are_ordered_by_organization_name()
    {
        var joined = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);
        var zeta = Member(Guid.CreateVersion7(), MemberStatus.Active, "Zeta", joined);
        var alfa = Member(Guid.CreateVersion7(), MemberStatus.Active, "Alfa", joined);

        var selected = AccessSwitchPolicy.SelectBusinessTenant([zeta, alfa], null, null);

        Assert.True(selected.IsSuccess);
        Assert.Equal(alfa.TenantId, selected.Value);
    }

    [Fact]
    public void Suspended_business_cannot_be_selected_explicitly()
    {
        var suspended = Member(Guid.CreateVersion7(), MemberStatus.Active, "Alfa", tenantStatus: TenantStatus.Suspended);

        var selected = AccessSwitchPolicy.SelectBusinessTenant([suspended], suspended.TenantId, null);

        Assert.Equal(TenantErrors.Suspended.Code, selected.Error.Code);
    }

    private static UserTenantAccessRow Member(Guid tenantId, MemberStatus status,
        string name = "Test", DateTime? joinedAtUtc = null, TenantStatus tenantStatus = TenantStatus.Active) =>
        new(tenantId, TenantKind.Business, name, null, tenantStatus, status, joinedAtUtc);
}
