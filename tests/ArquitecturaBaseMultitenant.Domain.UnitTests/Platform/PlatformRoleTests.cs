using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Platform;

public sealed class PlatformRoleTests
{
    [Fact]
    public void Owner_has_the_complete_platform_catalog()
    {
        Assert.Equal(PlatformPermissions.All.Order(), PlatformRolePermissions.For(PlatformRole.Owner).Order());
    }

    [Fact]
    public void Support_can_read_and_review_recoveries_without_managing_the_platform()
    {
        string[] expected =
        [
            PlatformPermissions.Tenants.Read,
            PlatformPermissions.Accounts.Read,
            PlatformPermissions.Recoveries.Manage,
            PlatformPermissions.Audit.Read,
        ];

        Assert.Equal(expected.Order(), PlatformRolePermissions.For(PlatformRole.Support).Order());
    }

    [Fact]
    public void Permission_collections_cannot_be_changed_by_callers()
    {
        var permissions = Assert.IsAssignableFrom<ICollection<string>>(PlatformRolePermissions.For(PlatformRole.Owner));

        Assert.True(permissions.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => permissions.Clear());
        Assert.Equal(PlatformPermissions.All.Order(), PlatformRolePermissions.For(PlatformRole.Owner).Order());
    }

    [Fact]
    public void Assignment_is_global_versioned_and_can_change_role()
    {
        var userId = Guid.CreateVersion7();
        var assignment = PlatformRoleAssignment.Create(userId, PlatformRole.Support);

        Assert.Equal(userId, assignment.UserId);
        Assert.Equal(PlatformRole.Support, assignment.Role);
        Assert.NotEqual(Guid.Empty, assignment.Id);
        Assert.IsAssignableFrom<IVersioned>(assignment);
        Assert.False(typeof(ITenantOwned).IsAssignableFrom(assignment.GetType()));

        assignment.ChangeRole(PlatformRole.Owner);

        Assert.Equal(PlatformRole.Owner, assignment.Role);
        Assert.Equal(userId, assignment.UserId);
    }

    [Fact]
    public void Assignment_rejects_invalid_technical_identifiers_and_roles()
    {
        Assert.Throws<ArgumentException>(() => PlatformRoleAssignment.Create(Guid.Empty, PlatformRole.Owner));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformRoleAssignment.Create(Guid.CreateVersion7(), (PlatformRole)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformRolePermissions.For((PlatformRole)99));
        var assignment = PlatformRoleAssignment.Create(Guid.CreateVersion7(), PlatformRole.Support);
        Assert.Throws<ArgumentOutOfRangeException>(() => assignment.ChangeRole((PlatformRole)99));
        Assert.Equal(PlatformRole.Support, assignment.Role);
    }
}
