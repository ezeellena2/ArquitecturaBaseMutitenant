using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Las columnas con una cuenta ordenan (decisión 264) con datos reales: los roles por sus personas y los permisos que
/// muestran, las empresas por sus miembros activos y las organizaciones de Plataforma por el Dueño que muestra la fila y
/// sus contadores. También las columnas calculadas de una invitación (su entrega y su vencimiento), de una cuenta (su
/// contacto, su último ingreso, su estado y su entrega) y de una recuperación (el nombre de su cuenta). Lo vacío va al final
/// en los dos sentidos y, con el mismo valor, sigue el orden de siempre (el nombre).
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

    [Fact]
    public async Task Invitations_sort_by_delivery_and_expiry_in_users_and_company_members_with_empty_values_last()
    {
        var tenantId = await OrganizationAsync("Orden de invitaciones " + Guid.NewGuid().ToString("N"));
        var ana = User("Ana");
        var anaMember = ActiveMember(ana);
        var company = Company.Create("Alfa", null, "UTC").Value;
        // Cómo salió cada invitación y en cuántos días vence. Beto y Dani vencen juntos y siguen el nombre; Dani no tiene
        // fila en la cola (enviada). Ana ya entró: no tiene invitación y va al final.
        var invited = new (string Name, OutboxStatus? Status, int Days)[]
        {
            ("Beto", OutboxStatus.Uncertain, 1), ("Carla", OutboxStatus.Pending, 3), ("Dani", null, 1), ("Eva", OutboxStatus.Failed, 2),
        }.Select(item =>
        {
            var user = User(item.Name);
            var member = Member.Invite(user.Id);
            var invitation = Invitation.Issue(member.Id, ana.Id, Email.Create($"{item.Name}.{Guid.NewGuid():N}@example.test").Value,
                "email", "hash-" + Guid.NewGuid().ToString("N"), NowUtc, TimeSpan.FromDays(item.Days));
            var outbox = item.Status is null ? null : OutboxMessage.Enqueue("email", "payload", NowUtc);
            if (outbox is not null) invitation.AttachOutboxMessage(outbox.Id);
            return (User: user, Member: member, Invitation: invitation, Outbox: outbox, item.Status);
        }).ToArray();
        await WriteAsync(tenantId, context =>
        {
            context.Users.Add(ana);
            context.Users.AddRange(invited.Select(item => item.User));
            context.Members.Add(anaMember);
            context.Members.AddRange(invited.Select(item => item.Member));
            context.OutboxMessages.AddRange(invited.Select(item => item.Outbox).OfType<OutboxMessage>());
            context.Invitations.AddRange(invited.Select(item => item.Invitation));
            context.Companies.Add(company);
            context.CompanyMemberships.AddRange(invited.Select(item => item.Member).Prepend(anaMember)
                .Select(member => CompanyMembership.Create(company.Id, member.Id).Value));
        });
        foreach (var item in invited.Where(item => item.Status is not (null or OutboxStatus.Pending)))
            await OutboxRows.SetStatusAsync(factory.Services, item.Outbox!.Id, item.Status!.Value, Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var users = scope.ServiceProvider.GetRequiredService<IOrganizationUserReader>();
        async Task<string[]> UserNames(string sort) => [.. (await users.ListAsync(
            new ListOrganizationUsersRequest { Sort = sort }, NowUtc, Ct)).Items.Select(user => user.DisplayName)];
        var members = scope.ServiceProvider.GetRequiredService<ICompanyMembershipReader>();
        async Task<string[]> MemberNames(string sort) => [.. (await members.ListAsync(company.Id,
            new ListCompanyMembersRequest { Sort = sort }, Ct)).Items.Select(member => member.DisplayName)];

        foreach (var names in new Func<string, Task<string[]>>[] { UserNames, MemberNames })
        {
            // La entrega sigue su circuito: en la cola, enviada, sin confirmar, no salió.
            Assert.Equal(["Carla", "Dani", "Beto", "Eva", "Ana"], await names("invitationDelivery"));
            Assert.Equal(["Eva", "Beto", "Dani", "Carla", "Ana"], await names("-invitationDelivery"));
            Assert.Equal(["Beto", "Dani", "Eva", "Carla", "Ana"], await names("invitationExpiresAtUtc"));
            Assert.Equal(["Carla", "Eva", "Beto", "Dani", "Ana"], await names("-invitationExpiresAtUtc"));
        }

        foreach (var sort in ListOrganizationUsersRequest.SortableFields.SelectMany(field => new[] { field, "-" + field }))
            Assert.Equal(5, (await UserNames(sort)).Length);
        foreach (var sort in ListCompanyMembersRequest.SortableFields.SelectMany(field => new[] { field, "-" + field }))
            Assert.Equal(5, (await MemberNames(sort)).Length);
    }

    [Fact]
    public async Task Platform_accounts_and_recoveries_sort_by_their_shown_values_with_empty_values_last()
    {
        var token = "ordencta" + Guid.NewGuid().ToString("N")[..10];
        var (ana, beto, ceci, dani) = (User("Ana " + token), User("Beto " + token), User("Ceci " + token), User("Dani " + token));
        // Ana: correo verificado, entró ayer. Beto: suspendido, con su teléfono principal, entró hace dos días. Ceci: invitada
        // como operadora, «Sin confirmar», con un ingreso que la fila no muestra. Dani: invitación vencida, sin fila en la
        // cola (enviada) y sin contacto.
        ana.Email = $"c-{token}@example.test";
        ceci.Email = $"a-{token}@example.test";
        Assert.True(beto.Suspend().IsSuccess);
        ceci.InvitePlatformOperator(NowUtc.AddDays(1));
        dani.InvitePlatformOperator(NowUtc.AddDays(-1));
        var invitation = OutboxMessage.Enqueue("email", "payload", NowUtc);
        ceci.RecordOperatorInvitationDelivery(invitation.Id);
        var anaEmail = LoginMethod.CreateEmail(ana.Id, Email.Create(ana.Email).Value);
        Assert.True(anaEmail.Verify(NowUtc).IsSuccess);
        var betoPhone = LoginMethod.CreatePhone(beto.Id,
            PhoneNumber.Create("+54911" + Random.Shared.Next(10_000_000, 99_999_999)).Value);
        Assert.True(betoPhone.Verify(NowUtc).IsSuccess);
        Assert.True(betoPhone.MakePrimary().IsSuccess);
        var newEmail = Email.Create($"new-{token}@example.test").Value;
        var recoveries = new[] { beto.Id, (Guid?)null, ana.Id }.Select(userId => AccountRecoveryRequest.Submit(
            Guid.CreateVersion7(), LoginMethodType.Email, $"old-{token}@example.test", newEmail, userId, NowUtc)).ToArray();
        await WriteAsync(null, context =>
        {
            context.Users.AddRange(ana, beto, ceci, dani);
            context.LoginMethods.AddRange(anaEmail, betoPhone);
            context.OutboxMessages.Add(invitation);
            context.LoginAudits.AddRange(LoginAudit.Success(ana.Id, LoginAuditMethod.Code, NowUtc.AddDays(-1)),
                LoginAudit.Success(ana.Id, LoginAuditMethod.Code, NowUtc.AddDays(-3)),
                LoginAudit.Success(beto.Id, LoginAuditMethod.Code, NowUtc.AddDays(-2)),
                LoginAudit.Success(ceci.Id, LoginAuditMethod.Code, NowUtc));
            context.AccountRecoveryRequests.AddRange(recoveries);
        });
        await OutboxRows.SetStatusAsync(factory.Services, invitation.Id, OutboxStatus.Uncertain, Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IPlatformAccountReader>();
        async Task<Guid[]> AccountIds(string sort) => [.. (await accounts.ListAsync(
            new ListPlatformAccountsRequest { Search = token, Sort = sort }, [LoginMethodType.Email, LoginMethodType.Phone],
            NowUtc, Ct)).Items.Select(account => account.Id)];
        var recoveryReader = scope.ServiceProvider.GetRequiredService<IAccountRecoveryReader>();
        async Task<Guid?[]> RecoveryAccounts(string sort) => [.. (await recoveryReader.ListAsync(
            new ListPlatformRecoveriesRequest { Search = token, Sort = sort }, Ct)).Items.Select(recovery => recovery.AccountId)];

        // El teléfono («+54…») va antes que los correos.
        Assert.Equal([beto.Id, ceci.Id, ana.Id, dani.Id], await AccountIds("contact"));
        Assert.Equal([ana.Id, ceci.Id, beto.Id, dani.Id], await AccountIds("-contact"));
        Assert.Equal([beto.Id, ana.Id, ceci.Id, dani.Id], await AccountIds("lastSignInAtUtc"));
        Assert.Equal([ana.Id, beto.Id, ceci.Id, dani.Id], await AccountIds("-lastSignInAtUtc"));
        // Activa, suspendida, invitada, invitación vencida: el orden del circuito.
        Assert.Equal([ana.Id, beto.Id, ceci.Id, dani.Id], await AccountIds("state"));
        Assert.Equal([dani.Id, ceci.Id, beto.Id, ana.Id], await AccountIds("-state"));
        Assert.Equal([dani.Id, ceci.Id, ana.Id, beto.Id], await AccountIds("invitationDelivery"));
        Assert.Equal([ceci.Id, dani.Id, ana.Id, beto.Id], await AccountIds("-invitationDelivery"));
        Assert.Equal([ana.Id, beto.Id, null], await RecoveryAccounts("accountName"));
        Assert.Equal([beto.Id, ana.Id, null], await RecoveryAccounts("-accountName"));

        foreach (var sort in ListPlatformAccountsRequest.SortableFields.SelectMany(field => new[] { field, "-" + field }))
            Assert.Equal(4, (await AccountIds(sort)).Length);
        foreach (var sort in ListPlatformRecoveriesRequest.SortableFields.SelectMany(field => new[] { field, "-" + field }))
            Assert.Equal(3, (await RecoveryAccounts(sort)).Length);
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
