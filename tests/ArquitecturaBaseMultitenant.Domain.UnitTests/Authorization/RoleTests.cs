using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authorization;

public sealed class RoleTests
{
    [Fact]
    public void Organization_role_accepts_permissions_from_the_organization_catalog()
    {
        var result = Role.Create("Lectores", "Consulta de administración", RoleScope.Organization, null,
            [Permissions.Roles.Read, Permissions.Users.Read]);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lectores", result.Value.Name);
        Assert.Equal(RoleScope.Organization, result.Value.Scope);
        Assert.Null(result.Value.CompanyId);
        Assert.False(result.Value.IsSystemRole);
        Assert.Contains(Permissions.Roles.Read, result.Value.Permissions);
        Assert.IsAssignableFrom<ITenantOwned>(result.Value);
        Assert.IsAssignableFrom<IVersioned>(result.Value);
    }

    [Fact]
    public void Create_trims_name_and_keeps_a_culture_independent_uniqueness_key()
    {
        var role = Role.Create("  Gestión diaria  ", null, RoleScope.Organization, null, []).Value;

        Assert.Equal("Gestión diaria", role.Name);
        Assert.Equal("GESTIÓN DIARIA", role.NormalizedName);
        var canonicalEquivalent = Role.Create("Gestio\u0301n diaria", null, RoleScope.Organization, null, []).Value;
        Assert.Equal(role.NormalizedName, canonicalEquivalent.NormalizedName);
        Assert.Equal(RoleErrors.SystemRoleNameReserved,
            Role.Create("tenantadmin", null, RoleScope.Organization, null, []).Error);
    }

    [Fact]
    public void Company_scoped_role_requires_a_company_only_for_specific_company_scope()
    {
        var companyId = Guid.CreateVersion7();

        var missingCompany = Role.Create("Gestores", null, RoleScope.SpecificCompany, null,
            [Permissions.Company.Members.Read]);
        var anyCompany = Role.Create("Administradores", null, RoleScope.AnyCompany, null,
            [Permissions.Company.Members.Manage]);
        var specificCompany = Role.Create("Lectores locales", null, RoleScope.SpecificCompany, companyId,
            [Permissions.Company.Members.Read]);

        Assert.Equal(RoleErrors.CompanyRequired, missingCompany.Error);
        Assert.True(anyCompany.IsSuccess);
        Assert.Null(anyCompany.Value.CompanyId);
        Assert.True(specificCompany.IsSuccess);
        Assert.Equal(companyId, specificCompany.Value.CompanyId);
    }

    [Fact]
    public void Role_scope_rejects_a_company_for_organization_or_any_company()
    {
        var companyId = Guid.CreateVersion7();

        var organization = Role.Create("Organización", null, RoleScope.Organization, companyId, []);
        var reusableCompany = Role.Create("Por empresa", null, RoleScope.AnyCompany, companyId,
            [Permissions.Company.Members.Read]);

        Assert.Equal(RoleErrors.CompanyNotAllowed, organization.Error);
        Assert.Equal(RoleErrors.CompanyNotAllowed, reusableCompany.Error);
    }

    [Fact]
    public void Role_rejects_names_or_descriptions_over_the_domain_limits()
    {
        var longName = new string('r', TextLimits.ShortName + 1);
        var longDescription = new string('d', TextLimits.Description + 1);

        Assert.Equal(RoleErrors.NameTooLong,
            Role.Create(longName, null, RoleScope.Organization, null, []).Error);
        Assert.Equal(RoleErrors.DescriptionTooLong,
            Role.Create("Auditores", longDescription, RoleScope.Organization, null, []).Error);
    }

    [Fact]
    public void Role_rejects_permissions_outside_the_catalog_or_company_catalog_for_company_scopes()
    {
        var unknown = Role.Create("Inventado", null, RoleScope.Organization, null, ["unknown.permission"]);
        var wrongScope = Role.Create("Gestores", null, RoleScope.AnyCompany, null, [Permissions.Roles.Manage]);

        Assert.Equal(RoleErrors.PermissionNotFound, unknown.Error);
        Assert.Equal(RoleErrors.PermissionScopeInvalid, wrongScope.Error);
    }

    [Fact]
    public void The_scope_changes_when_it_applies_somewhere_else_but_not_when_only_the_name_changes()
    {
        var north = Guid.CreateVersion7();
        var role = Role.Create("Lectores Norte", null, RoleScope.SpecificCompany, north,
            [Permissions.Company.Members.Read]).Value;

        Assert.False(role.ChangesScope(RoleScope.SpecificCompany, north));
        Assert.True(role.ChangesScope(RoleScope.SpecificCompany, Guid.CreateVersion7()));
        Assert.True(role.ChangesScope(RoleScope.AnyCompany, null));
        Assert.True(role.ChangesScope(RoleScope.Organization, null));
        Assert.Equal(RoleErrors.ScopeChangeHasUsersCode, RoleErrors.ScopeChangeHasUsers(3).Code);
        Assert.Equal(3, RoleErrors.ScopeChangeHasUsers(3).Metadata![RoleErrors.UserCountKey]);
    }

    [Fact]
    public void System_roles_are_immutable_and_cover_their_access_scopes()
    {
        var tenantAdmin = SystemRoles.CreateTenantAdmin();
        var companyAdmin = SystemRoles.CreateCompanyAdmin();

        var update = tenantAdmin.Update("Otro nombre", null, RoleScope.Organization, null, []);

        Assert.Equal(RoleErrors.SystemRoleCannotChange, update.Error);
        Assert.Equal(SystemRoles.TenantAdmin, tenantAdmin.Name);
        Assert.True(tenantAdmin.IsSystemRole);
        Assert.Equal(Permissions.All, tenantAdmin.Permissions);
        Assert.True(companyAdmin.IsSystemRole);
        Assert.Equal(RoleScope.AnyCompany, companyAdmin.Scope);
        Assert.Equal([.. Permissions.CompanyScoped, Permissions.Assistant.Use], companyAdmin.Permissions);
        Assert.Contains(Permissions.Assistant.Configure, tenantAdmin.Permissions);
        Assert.DoesNotContain(Permissions.Assistant.Configure, companyAdmin.Permissions);
        Assert.DoesNotContain(Permissions.Assistant.Use, Permissions.CompanyScoped);
        Assert.Equal([SystemRoles.TenantAdmin, SystemRoles.CompanyAdmin], SystemRoles.All);
    }
}
