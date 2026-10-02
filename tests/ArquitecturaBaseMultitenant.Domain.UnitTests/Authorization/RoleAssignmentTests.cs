using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authorization;

public sealed class RoleAssignmentTests
{
    [Fact]
    public void Organization_role_is_assigned_without_a_company()
    {
        var memberId = Guid.CreateVersion7();
        var role = Role.Create("Dueño adjunto", null, RoleScope.Organization, null, Permissions.All).Value;

        var result = RoleAssignment.Assign(memberId, role, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(memberId, result.Value.MemberId);
        Assert.Equal(role.Id, result.Value.RoleId);
        Assert.Null(result.Value.CompanyId);
    }

    [Fact]
    public void Any_company_role_requires_the_selected_company()
    {
        var memberId = Guid.CreateVersion7();
        var companyId = Guid.CreateVersion7();
        var role = Role.Create("Administrador", null, RoleScope.AnyCompany, null,
            [Permissions.Company.Members.Manage]).Value;

        var missingCompany = RoleAssignment.Assign(memberId, role, null);
        var assigned = RoleAssignment.Assign(memberId, role, companyId);

        Assert.Equal(RoleAssignmentErrors.CompanyRequired, missingCompany.Error);
        Assert.True(assigned.IsSuccess);
        Assert.Equal(companyId, assigned.Value.CompanyId);
    }

    [Fact]
    public void Specific_company_role_can_only_be_assigned_to_its_company()
    {
        var memberId = Guid.CreateVersion7();
        var companyId = Guid.CreateVersion7();
        var otherCompanyId = Guid.CreateVersion7();
        var role = Role.Create("Local", null, RoleScope.SpecificCompany, companyId,
            [Permissions.Company.Members.Read]).Value;

        var otherCompany = RoleAssignment.Assign(memberId, role, otherCompanyId);
        var assigned = RoleAssignment.Assign(memberId, role, companyId);

        Assert.Equal(RoleAssignmentErrors.CompanyMismatch, otherCompany.Error);
        Assert.True(assigned.IsSuccess);
        Assert.Equal(companyId, assigned.Value.CompanyId);
    }

    [Fact]
    public void Company_assignment_is_rejected_for_an_organization_role()
    {
        var memberId = Guid.CreateVersion7();
        var role = Role.Create("Lectores", null, RoleScope.Organization, null, [Permissions.Roles.Read]).Value;

        var result = RoleAssignment.Assign(memberId, role, Guid.CreateVersion7());

        Assert.Equal(RoleAssignmentErrors.CompanyNotAllowed, result.Error);
    }
}
