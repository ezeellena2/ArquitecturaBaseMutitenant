using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authorization;

public sealed class RoleAssignmentTests
{
    private static readonly Guid CompanyA = Guid.CreateVersion7();
    private static readonly Guid CompanyB = Guid.CreateVersion7();

    /// <summary>La matriz completa de dónde vale un rol: alcance × empresa del rol × dónde se asigna.</summary>
    public static TheoryData<RoleScope, Guid?, Guid?, string?> Matrix => new()
    {
        { RoleScope.Organization, null, null, null },
        { RoleScope.Organization, null, CompanyA, RoleAssignmentErrors.CompanyNotAllowedCode },
        { RoleScope.AnyCompany, null, null, RoleAssignmentErrors.CompanyRequiredCode },
        { RoleScope.AnyCompany, null, CompanyA, null },
        { RoleScope.AnyCompany, null, CompanyB, null },
        { RoleScope.SpecificCompany, CompanyA, null, RoleAssignmentErrors.CompanyRequiredCode },
        { RoleScope.SpecificCompany, CompanyA, CompanyA, null },
        { RoleScope.SpecificCompany, CompanyA, CompanyB, RoleAssignmentErrors.CompanyMismatchCode },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Check_answers_where_each_role_scope_can_be_assigned(RoleScope scope, Guid? roleCompanyId,
        Guid? companyId, string? expectedCode)
    {
        Assert.Equal(expectedCode, RoleAssignment.Check(scope, roleCompanyId, companyId)?.Code);
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Assign_follows_the_same_matrix_without_loading_the_role(RoleScope scope, Guid? roleCompanyId,
        Guid? companyId, string? expectedCode)
    {
        var memberId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();

        var result = RoleAssignment.Assign(memberId, roleId, scope, roleCompanyId, companyId);

        if (expectedCode is not null)
        {
            Assert.Equal(expectedCode, result.Error.Code);
            return;
        }

        Assert.True(result.IsSuccess);
        Assert.Equal(memberId, result.Value.MemberId);
        Assert.Equal(roleId, result.Value.RoleId);
        Assert.Equal(companyId, result.Value.CompanyId);
    }

    [Fact]
    public void Assign_takes_the_scope_of_a_created_role()
    {
        var role = Role.Create("Local", null, RoleScope.SpecificCompany, CompanyA,
            [Permissions.Company.Members.Read]).Value;

        var assigned = RoleAssignment.Assign(Guid.CreateVersion7(), role.Id, role.Scope, role.CompanyId, CompanyA);

        Assert.Equal(role.Id, assigned.Value.RoleId);
        Assert.Equal(CompanyA, assigned.Value.CompanyId);
    }
}
