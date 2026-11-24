using ArquitecturaBaseMultitenant.Application.Services.Companies;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Roles;

/// <summary>
/// Nadie da lo que no tiene (ADR 0061), en la organización y en una empresa, con lo efectivo de quien pide (decisión
/// 260): las <c>Policy</c> puras que usan las guardas, los listados (<c>grantable</c>) y las acciones del asistente.
/// </summary>
public sealed class OrganizationDelegationPolicyTests
{
    private static readonly Guid North = Guid.CreateVersion7();
    private static readonly Guid South = Guid.CreateVersion7();

    [Fact]
    public void The_owner_gives_takes_away_and_acts_on_anything()
    {
        var owner = Held(Permissions.All);
        var anyone = Held([Permissions.Settings.Manage], (North, [Permissions.Company.Members.Manage]));

        Assert.True(OrganizationDelegationPolicy.CanChangePermissions(owner, [], Permissions.All));
        Assert.True(OrganizationDelegationPolicy.CanChangePermissions(owner, Permissions.All, []));
        Assert.True(OrganizationDelegationPolicy.CanGrantRole(owner, Permissions.All, companyId: null));
        Assert.True(OrganizationDelegationPolicy.CanGrantRole(owner, Permissions.CompanyScoped, South));
        Assert.True(OrganizationDelegationPolicy.CanActOn(self: false, owner, anyone));
        Assert.All(Permissions.All, code => Assert.True(OrganizationDelegationPolicy.CanGrantPermission(owner, code)));
    }

    [Fact]
    public void A_role_change_counts_every_permission_that_comes_in_or_goes_out_with_what_it_includes()
    {
        var roles = Held([Permissions.Roles.Manage]);

        // Lo que tiene, con lo incluido: «Administrar roles» trae «Ver roles».
        Assert.True(OrganizationDelegationPolicy.CanChangePermissions(roles, [], [Permissions.Roles.Read]));
        Assert.True(OrganizationDelegationPolicy.CanChangePermissions(roles, [], [Permissions.Roles.Manage]));
        // Sumar o sacar uno que no tiene, no; tampoco a través de lo que incluye.
        Assert.False(OrganizationDelegationPolicy.CanChangePermissions(roles, [], [Permissions.Settings.Read]));
        Assert.False(OrganizationDelegationPolicy.CanChangePermissions(roles,
            [Permissions.Settings.Read, Permissions.Roles.Read], [Permissions.Roles.Read]));
        Assert.False(OrganizationDelegationPolicy.CanChangePermissions(roles, [], [Permissions.Users.Manage]));
        // Lo que no cambia no cuenta, aunque no lo tenga: el nombre y la descripción se editan igual.
        Assert.True(OrganizationDelegationPolicy.CanChangePermissions(roles,
            [Permissions.Settings.Manage, Permissions.Settings.Read], [Permissions.Settings.Read, Permissions.Settings.Manage]));
        // Un rol viejo guardado sin lo incluido cambia igual que uno completo: se compara lo efectivo.
        Assert.True(OrganizationDelegationPolicy.CanChangePermissions(Held([Permissions.Users.Manage]),
            [Permissions.Users.Manage], [Permissions.Users.Manage, Permissions.Users.Read]));
    }

    [Fact]
    public void A_permission_outside_the_catalog_neither_gives_nor_asks_for_anything()
    {
        const string removedModule = "removed.module.manage";
        var roles = Held([Permissions.Roles.Manage, removedModule]);

        Assert.True(OrganizationDelegationPolicy.CanChangePermissions(roles, [removedModule], []));
        Assert.False(OrganizationDelegationPolicy.CanGrantPermission(roles, removedModule));
        Assert.DoesNotContain(removedModule, roles.Organization);
    }

    [Fact]
    public void In_a_company_the_requester_holds_its_organization_roles_plus_the_ones_of_that_company()
    {
        var admin = Held([Permissions.Users.Manage],
            (North, [Permissions.Company.Members.Manage, Permissions.Assistant.Use]));

        Assert.True(OrganizationDelegationPolicy.CanGrantRole(admin, [Permissions.Company.Members.Read], North));
        Assert.True(OrganizationDelegationPolicy.CanGrantRole(admin, [Permissions.Users.Read], North));
        Assert.False(OrganizationDelegationPolicy.CanGrantRole(admin, [Permissions.Company.Members.Read], South));
        // En la organización no cuenta lo de una empresa, salvo lo que una empresa proyecta: «Usar el asistente».
        Assert.False(OrganizationDelegationPolicy.CanGrantRole(admin, [Permissions.Company.Members.Read], companyId: null));
        Assert.True(OrganizationDelegationPolicy.CanGrantRole(admin, [Permissions.Assistant.Use], companyId: null));
        Assert.True(OrganizationDelegationPolicy.CanGrantPermission(admin, Permissions.Assistant.Use));
        Assert.False(OrganizationDelegationPolicy.CanGrantPermission(admin, Permissions.Company.Members.Manage));
        Assert.True(CompanyDelegationPolicy.CanChangeRoles(admin, North,
            [[Permissions.Company.Members.Manage], [Permissions.Users.Read]]));
        Assert.False(CompanyDelegationPolicy.CanChangeRoles(admin, North,
            [[Permissions.Company.Members.Read], [Permissions.Settings.Read]]));
        Assert.False(CompanyDelegationPolicy.CanGrantRole(admin, South, [Permissions.Company.Members.Read]));
    }

    [Fact]
    public void A_listed_role_is_grantable_where_it_applies()
    {
        var admin = Held([Permissions.Users.Read], (North, [Permissions.Company.Members.Read]));
        string[] members = [Permissions.Company.Members.Read];

        Assert.True(OrganizationDelegationPolicy.CanGrantListedRole(admin, RoleScope.SpecificCompany, North, members, null));
        Assert.False(OrganizationDelegationPolicy.CanGrantListedRole(admin, RoleScope.SpecificCompany, South, members, North));
        // Uno de cualquier empresa: en la del listado, o en todas si no hay.
        Assert.True(OrganizationDelegationPolicy.CanGrantListedRole(admin, RoleScope.AnyCompany, null, members, North));
        Assert.False(OrganizationDelegationPolicy.CanGrantListedRole(admin, RoleScope.AnyCompany, null, members, null));
        Assert.True(OrganizationDelegationPolicy.CanGrantListedRole(admin, RoleScope.Organization, null,
            [Permissions.Users.Read], North));
        Assert.False(OrganizationDelegationPolicy.CanGrantListedRole(admin, RoleScope.Organization, null, members, North));
    }

    [Fact]
    public void Changing_roles_asks_for_each_role_that_comes_in_or_goes_out_where_it_does()
    {
        var helpDesk = Held([Permissions.Users.Manage, Permissions.Roles.Assign]);

        Assert.True(OrganizationDelegationPolicy.CanChangeRoles(helpDesk,
            [new([Permissions.Users.Read], null), new([Permissions.Roles.Read, Permissions.Roles.Assign], null)]));
        Assert.False(OrganizationDelegationPolicy.CanChangeRoles(helpDesk,
            [new([Permissions.Users.Read], null), new(Permissions.All, null)]));
        Assert.False(OrganizationDelegationPolicy.CanChangeRoles(helpDesk,
            [new([Permissions.Company.Members.Read], North)]));
        Assert.True(OrganizationDelegationPolicy.CanChangeRoles(helpDesk, []));
    }

    [Fact]
    public void Acting_on_someone_asks_for_all_their_permissions_in_each_place_except_on_oneself()
    {
        var helpDesk = Held([Permissions.Users.Manage], (North, [Permissions.Company.Members.Manage]));

        Assert.True(OrganizationDelegationPolicy.CanActOn(self: false, helpDesk, Held([Permissions.Users.Read])));
        Assert.True(OrganizationDelegationPolicy.CanActOn(self: false, helpDesk,
            Held([], (North, [Permissions.Company.Members.Read]))));
        Assert.False(OrganizationDelegationPolicy.CanActOn(self: false, helpDesk, Held([Permissions.Settings.Read])));
        Assert.False(OrganizationDelegationPolicy.CanActOn(self: false, helpDesk,
            Held([], (South, [Permissions.Company.Members.Read]))));
        Assert.False(OrganizationDelegationPolicy.CanActOn(self: false, helpDesk, Held(Permissions.All)));
        Assert.True(OrganizationDelegationPolicy.CanActOn(self: true, helpDesk, Held(Permissions.All)));
    }

    [Fact]
    public void In_a_company_acting_on_someone_counts_their_organization_roles_too()
    {
        var admin = Held([], (North, [Permissions.Company.Members.Manage, Permissions.Assistant.Use]));

        Assert.True(CompanyDelegationPolicy.CanActOn(self: false, admin, North,
            Held([], (North, [Permissions.Company.Members.Manage]), (South, [Permissions.Assistant.Use]))));
        Assert.False(CompanyDelegationPolicy.CanActOn(self: false, admin, North,
            Held([Permissions.Users.Read], (North, [Permissions.Company.Members.Read]))));
        Assert.False(CompanyDelegationPolicy.CanActOn(self: false, Held([], (North, [Permissions.Company.Members.Manage])),
            North, Held([], (North, [Permissions.Company.Members.Manage, Permissions.Assistant.Use]))));
        Assert.True(CompanyDelegationPolicy.CanActOn(self: true, admin, North, Held(Permissions.All)));
    }

    private static PermissionsByPlace Held(IEnumerable<string> organization,
        params (Guid CompanyId, string[] Permissions)[] companies) =>
        PermissionsByPlace.Of(organization,
            companies.ToDictionary(company => company.CompanyId, company => (IEnumerable<string>)company.Permissions));
}
