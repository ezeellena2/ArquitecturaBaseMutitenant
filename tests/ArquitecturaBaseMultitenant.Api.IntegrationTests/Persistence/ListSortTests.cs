using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Las columnas con una cuenta ordenan (decisión 264) con datos reales: los roles por sus personas y los permisos que
/// muestran, las empresas por sus miembros activos y las organizaciones de Plataforma por el Dueño que muestra la fila y
/// sus contadores. Lo vacío va al final en los dos sentidos y, con el mismo valor, sigue el orden de siempre (el nombre).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ListSortTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Roles_and_companies_sort_by_their_counts_in_both_directions_with_the_name_as_tie_breaker()
    {
        var tenantId = await OrganizationAsync("Orden de listas " + Guid.NewGuid().ToString("N"));
        var (ana, beto) = (User("Ana"), User("Beto"));
        var (anaMember, betoMember) = (ActiveMember(ana), ActiveMember(beto));
        var companies = new[] { "Alfa", "Beta", "Delta", "Gama" }.Select(name => Company.Create(name, null, "UTC").Value).ToArray();
        var (alfa, beta, delta, gama) = (companies[0], companies[1], companies[2], companies[3]);
        var roleA = Role.Create("Orden A", null, RoleScope.Organization, null,
            [Permissions.Users.Read, Permissions.Roles.Read, Permissions.Companies.Read]).Value;
        var roleB = Role.Create("Orden B", null, RoleScope.Organization, null, [Permissions.Users.Read]).Value;
        var roleC = Role.Create("Orden C", null, RoleScope.AnyCompany, null, [Permissions.Company.Members.Read]).Value;
        // De sistema: trae un permiso de la organización que su fila no muestra, así que cuenta 2 y no 3.
        var companyAdmin = SystemRoles.CreateCompanyAdmin();
        await WriteAsync(tenantId, context =>
        {
            context.Users.AddRange(ana, beto);
            context.Members.AddRange(anaMember, betoMember);
            context.Companies.AddRange(companies);
            // Alfa: dos miembros activos; Gama: uno; Beta y Delta: ninguno (la membresía inactiva no cuenta).
            context.CompanyMemberships.AddRange(CompanyMembership.Create(alfa.Id, anaMember.Id).Value,
                CompanyMembership.Create(alfa.Id, betoMember.Id).Value, CompanyMembership.Create(gama.Id, anaMember.Id).Value);
            var inactive = CompanyMembership.Create(delta.Id, betoMember.Id).Value;
            inactive.SetStatus(CompanyMembershipStatus.Inactive);
            context.CompanyMemberships.Add(inactive);
            context.Roles.AddRange(roleA, roleB, roleC, companyAdmin);
            // Personas: B dos, A y C una, CompanyAdmin ninguna.
            context.RoleAssignments.AddRange(
                RoleAssignment.Assign(anaMember.Id, roleB.Id, RoleScope.Organization, null, null).Value,
                RoleAssignment.Assign(betoMember.Id, roleB.Id, RoleScope.Organization, null, null).Value,
                RoleAssignment.Assign(anaMember.Id, roleA.Id, RoleScope.Organization, null, null).Value,
                RoleAssignment.Assign(anaMember.Id, roleC.Id, RoleScope.AnyCompany, null, alfa.Id).Value);
        });

        await using var scope = factory.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var roles = scope.ServiceProvider.GetRequiredService<IRoleReader>();
        async Task<string[]> RoleNames(string sort) =>
            [.. (await roles.ListAsync(new ListRolesRequest { Sort = sort }, Ct)).Items.Select(role => role.Name)];
        var companyReader = scope.ServiceProvider.GetRequiredService<ICompanyReader>();
        async Task<string[]> CompanyNames(string sort) =>
            [.. (await companyReader.ListAsync(new ListCompaniesRequest { Sort = sort }, Ct)).Items.Select(company => company.Name)];

        Assert.Equal([SystemRoles.CompanyAdmin, "Orden A", "Orden C", "Orden B"], await RoleNames("userCount"));
        Assert.Equal(["Orden B", "Orden A", "Orden C", SystemRoles.CompanyAdmin], await RoleNames("-userCount"));
        Assert.Equal(["Orden B", "Orden C", SystemRoles.CompanyAdmin, "Orden A"], await RoleNames("permissions"));
        Assert.Equal(["Orden A", SystemRoles.CompanyAdmin, "Orden B", "Orden C"], await RoleNames("-permissions"));
        Assert.Equal(["Beta", "Delta", "Gama", "Alfa"], await CompanyNames("memberCount"));
        Assert.Equal(["Alfa", "Gama", "Beta", "Delta"], await CompanyNames("-memberCount"));

        // Cada campo que admite la consulta corre en los dos sentidos y trae la página entera.
        foreach (var sort in ListRolesRequest.SortableFields.SelectMany(field => new[] { field, "-" + field }))
            Assert.Equal(4, (await RoleNames(sort)).Length);
        foreach (var sort in ListCompaniesRequest.SortableFields.SelectMany(field => new[] { field, "-" + field }))
            Assert.Equal(4, (await CompanyNames(sort)).Length);
    }

    [Fact]
    public async Task Platform_organizations_sort_by_owner_and_counters_with_empty_values_last()
    {
        var token = "Ordenplat" + Guid.NewGuid().ToString("N")[..10];
        var (ana, beto) = (User("Ana " + token), User("Beto " + token));
        await WriteAsync(null, context => context.Users.AddRange(ana, beto));
        // Uno: lo registró Beto, dos empresas, sin miembros. Dos: lo registró Ana después, sin empresas, un miembro.
        // Tres: sin Dueño ni alta, una empresa: su Dueño y su alta quedan vacíos y van al final.
        var uno = await OrganizationAsync(token + " Uno", beto.Id, NowUtc.AddDays(-2));
        var dos = await OrganizationAsync(token + " Dos", ana.Id, NowUtc.AddDays(-1));
        var tres = await OrganizationAsync(token + " Tres");
        await WriteAsync(uno, context => context.Companies.AddRange(Company.Create("Uno A", null, "UTC").Value,
            Company.Create("Uno B", null, "UTC").Value));
        await WriteAsync(dos, context => context.Members.Add(ActiveMember(ana)));
        await WriteAsync(tres, context => context.Companies.Add(Company.Create("Tres A", null, "UTC").Value));

        await using var scope = factory.Services.CreateAsyncScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformReader>();
        async Task<Guid[]> Ids(string sort) => [.. (await platform.ListTenantsAsync(
            new ListPlatformTenantsRequest { Search = token, Sort = sort }, Ct)).Items.Select(tenant => tenant.Id)];

        Assert.Equal([dos, uno, tres], await Ids("ownerName"));
        Assert.Equal([uno, dos, tres], await Ids("-ownerName"));
        Assert.Equal([uno, dos, tres], await Ids("createdAtUtc"));
        Assert.Equal([dos, uno, tres], await Ids("-createdAtUtc"));
        Assert.Equal([dos, tres, uno], await Ids("companyCount"));
        Assert.Equal([uno, tres, dos], await Ids("-companyCount"));
        // Uno y Tres no tienen miembros: siguen el nombre, «Tres» antes que «Uno».
        Assert.Equal([tres, uno, dos], await Ids("memberCount"));
        Assert.Equal([dos, tres, uno], await Ids("-memberCount"));

        foreach (var sort in ListPlatformTenantsRequest.SortableFields.SelectMany(field => new[] { field, "-" + field }))
            Assert.Equal(3, (await Ids(sort)).Length);
    }

    private static ApplicationUser User(string name) => ApplicationUser.Create(name, "es-AR", "UTC", NowUtc).Value;

    private static Member ActiveMember(ApplicationUser user)
    {
        var member = Member.Invite(user.Id);
        Assert.True(member.Activate(NowUtc).IsSuccess);
        return member;
    }

    private async Task<Guid> OrganizationAsync(string name, Guid? registeredBy = null, DateTime? createdAtUtc = null)
    {
        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        if (registeredBy is { } userId)
            Assert.True(tenant.RecordRegistration(userId, TenantRegistrationSource.SelfService, createdAtUtc!.Value).IsSuccess);
        await WriteAsync(null, context => context.Tenants.Add(tenant));
        return tenant.Id;
    }

    private async Task WriteAsync(Guid? tenantId, Action<ApplicationDbContext> write)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = tenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            write(services.GetRequiredService<ApplicationDbContext>());
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }
}
