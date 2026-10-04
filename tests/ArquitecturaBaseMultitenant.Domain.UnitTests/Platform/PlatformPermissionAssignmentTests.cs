using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Platform;

/// <summary>Permisos individuales de un operador: solo claves del catálogo, sin repetir y al menos una.</summary>
public sealed class PlatformPermissionAssignmentTests
{
    [Fact]
    public void Assignment_is_global_versioned_and_keeps_only_the_chosen_permissions_in_catalog_order()
    {
        var userId = Guid.CreateVersion7();

        var created = PlatformPermissionAssignment.Create(userId,
            [PlatformPermissions.Audit.Read, PlatformPermissions.Tenants.Read]);

        Assert.True(created.IsSuccess);
        var assignment = created.Value;
        Assert.Equal(userId, assignment.UserId);
        Assert.NotEqual(Guid.Empty, assignment.Id);
        Assert.Equal(new[] { PlatformPermissions.Tenants.Read, PlatformPermissions.Audit.Read }, assignment.Permissions);
        Assert.True(assignment.Grants(PlatformPermissions.Audit.Read));
        Assert.False(assignment.Grants(PlatformPermissions.Settings.Manage));
        Assert.IsAssignableFrom<IVersioned>(assignment);
        Assert.False(typeof(ITenantOwned).IsAssignableFrom(assignment.GetType()));
    }

    [Fact]
    public void The_complete_catalog_can_be_assigned_and_changed_to_a_subset()
    {
        var assignment = PlatformPermissionAssignment.Create(Guid.CreateVersion7(), PlatformPermissions.All).Value;
        Assert.Equal(PlatformPermissions.All, assignment.Permissions);

        var changed = assignment.ChangePermissions([PlatformPermissions.Recoveries.Manage]);

        Assert.True(changed.IsSuccess);
        Assert.Equal(new[] { PlatformPermissions.Recoveries.Manage }, assignment.Permissions);
    }

    public static TheoryData<string[], string> InvalidSets => new()
    {
        { [], PlatformOperatorErrors.PermissionsRequiredCode },
        { ["platform.unknown"], PlatformOperatorErrors.PermissionNotFoundCode },
        { [Permissions.Users.Read], PlatformOperatorErrors.PermissionNotFoundCode },
        { [PlatformPermissions.Tenants.Read, PlatformPermissions.Tenants.Read], PlatformOperatorErrors.PermissionRepeatedCode },
    };

    [Theory]
    [MemberData(nameof(InvalidSets))]
    public void Invalid_sets_are_rejected_without_changing_the_assignment(string[] permissions, string code)
    {
        Assert.Equal(code, PlatformPermissionAssignment.Create(Guid.CreateVersion7(), permissions).Error.Code);
        var assignment = PlatformPermissionAssignment.Create(Guid.CreateVersion7(), [PlatformPermissions.Audit.Read]).Value;

        var changed = assignment.ChangePermissions(permissions);

        Assert.Equal(code, changed.Error.Code);
        Assert.Equal(new[] { PlatformPermissions.Audit.Read }, assignment.Permissions);
    }

    [Fact]
    public void Null_sets_and_empty_users_are_rejected()
    {
        Assert.Equal(PlatformOperatorErrors.PermissionsRequiredCode,
            PlatformPermissionAssignment.Create(Guid.CreateVersion7(), null).Error.Code);
        Assert.Throws<ArgumentException>(() => PlatformPermissionAssignment.Create(Guid.Empty, PlatformPermissions.All));
    }

    [Fact]
    public void Permission_collection_cannot_be_changed_by_callers()
    {
        var assignment = PlatformPermissionAssignment.Create(Guid.CreateVersion7(), PlatformPermissions.All).Value;
        var permissions = Assert.IsAssignableFrom<ICollection<string>>(assignment.Permissions);

        Assert.True(permissions.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => permissions.Clear());
        Assert.Equal(PlatformPermissions.All, assignment.Permissions);
    }
}
