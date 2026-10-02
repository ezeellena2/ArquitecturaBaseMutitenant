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
        Assert.Equal(Permissions.CompanyScoped, companyAdmin.Permissions);
        Assert.Equal([SystemRoles.TenantAdmin, SystemRoles.CompanyAdmin], SystemRoles.All);
    }
}
