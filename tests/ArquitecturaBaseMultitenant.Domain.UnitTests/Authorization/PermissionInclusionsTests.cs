using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authorization;

/// <summary>
/// Un permiso incluye a otro (decisión 260): lo efectivo es lo asignado del catálogo más lo que incluye, en cadena, y el
/// catálogo rechaza al componerse lo incluido de otra área, de otro catálogo o que no existe.
/// </summary>
public sealed class PermissionInclusionsTests
{
    [Fact]
    public void Administer_without_view_allows_view_in_each_catalog()
    {
        Assert.Equal([Permissions.Users.Read, Permissions.Users.Manage],
            Permissions.Inclusions.Effective([Permissions.Users.Manage]));
        Assert.Equal([Permissions.Company.Members.Read, Permissions.Company.Members.Manage],
            Permissions.Inclusions.Effective([Permissions.Company.Members.Manage]));
        Assert.Equal([PlatformPermissions.Accounts.Read, PlatformPermissions.Accounts.Manage],
            PlatformPermissions.Inclusions.Effective([PlatformPermissions.Accounts.Manage]));
    }

    [Fact]
    public void The_pairs_of_the_decision_are_declared_and_nothing_else()
    {
        Assert.Equal(new Dictionary<string, string[]>
        {
            ["users.invite"] = ["users.read"],
            ["users.manage"] = ["users.read"],
            ["roles.manage"] = ["roles.read"],
            ["roles.assign"] = ["roles.read"],
            ["companies.manage"] = ["companies.read"],
            ["settings.manage"] = ["settings.read"],
            ["company.members.manage"] = ["company.members.read"],
        }, Permissions.All.Where(code => Permissions.Inclusions.Of(code).Count > 0)
            .ToDictionary(code => code, code => Permissions.Inclusions.Of(code).ToArray()));
        Assert.Equal(new Dictionary<string, string[]>
        {
            ["platform.tenants.manage"] = ["platform.tenants.read"],
            ["platform.accounts.manage"] = ["platform.accounts.read"],
            ["platform.assistant.configure"] = ["platform.assistant.read"],
        }, PlatformPermissions.All.Where(code => PlatformPermissions.Inclusions.Of(code).Count > 0)
            .ToDictionary(code => code, code => PlatformPermissions.Inclusions.Of(code).ToArray()));
        Assert.Equal(Permissions.All, Permissions.Inclusions.All);
        Assert.Equal(PlatformPermissions.All, PlatformPermissions.Inclusions.All);
    }

    [Fact]
    public void Inclusions_chain_in_catalog_order_and_a_cycle_ends()
    {
        var catalog = PermissionInclusions.Compose([["a.read", "a.edit", "a.manage", "a.audit"]],
            Includes(("a.manage", ["a.edit"]), ("a.edit", ["a.read"]), ("a.read", ["a.manage"])));

        Assert.Equal(["a.read", "a.edit"], catalog.Of("a.manage"));
        Assert.Equal(["a.read", "a.manage"], catalog.Of("a.edit"));
        Assert.Equal(["a.read", "a.edit", "a.manage"], catalog.Effective(["a.manage"]));
        Assert.Empty(catalog.Of("a.audit"));
        Assert.Empty(catalog.Of("b.unknown"));
    }

    [Fact]
    public void A_code_outside_the_catalog_allows_nothing_but_is_kept_when_saving()
    {
        Assert.Equal([Permissions.Roles.Read], Permissions.Inclusions.Effective(["roles.read", "whatsapp.removed"]));
        Assert.Empty(PlatformPermissions.Inclusions.Effective([Permissions.Users.Manage]));
        Assert.Equal(["users.manage", "whatsapp.removed", "users.read"],
            Permissions.Inclusions.WithIncluded(["users.manage", "whatsapp.removed", "users.manage"]));
    }

    [Fact]
    public void The_catalog_rejects_including_another_area()
    {
        var error = Assert.Throws<InvalidOperationException>(() => PermissionInclusions.Compose(
            [["users.read", "users.manage", "roles.read"]], Includes(("users.manage", ["roles.read"]))));
        Assert.Contains("area", error.Message, StringComparison.Ordinal);

        // El área es todo el prefijo: company.members.* no incluye company.*.
        Assert.Throws<InvalidOperationException>(() => PermissionInclusions.Compose(
            [["company.read", "company.members.manage"]], Includes(("company.members.manage", ["company.read"]))));
    }

    [Fact]
    public void The_catalog_rejects_including_another_catalog_or_a_missing_code()
    {
        var otherCatalog = Assert.Throws<InvalidOperationException>(() => PermissionInclusions.Compose(
            [["users.read"], ["users.manage"]], Includes(("users.manage", ["users.read"]))));
        Assert.Contains("catalog", otherCatalog.Message, StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => PermissionInclusions.Compose(
            [["users.manage"]], Includes(("users.manage", ["users.read"]))));
        Assert.Throws<InvalidOperationException>(() => PermissionInclusions.Compose(
            [["users.read"]], Includes(("users.manage", ["users.read"]))));
        Assert.Throws<InvalidOperationException>(() => PermissionInclusions.Compose(
            [["users.read"], ["users.read"]], Includes()));
    }

    private static Dictionary<string, IReadOnlyCollection<string>> Includes(
        params (string Code, string[] Included)[] pairs) =>
        pairs.ToDictionary(pair => pair.Code, pair => (IReadOnlyCollection<string>)pair.Included, StringComparer.Ordinal);
}
