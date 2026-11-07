using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Services.Home;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// «Tus empresas» de Inicio-Miembro cuenta los miembros de todas las empresas en una sola consulta: el mismo número que el
/// filtro «Activos» de Miembros de cada empresa, y null donde la persona no puede ver los miembros.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class MemberHomeCompaniesTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Member_counts_of_every_company_come_from_one_query_and_match_the_active_filter()
    {
        var tenantId = await OrganizationAsync();
        var viewer = User("Quien mira");
        var colleagues = new[] { User("Colega uno"), User("Colega dos"), User("Colega tres") };
        var suspended = User("Suspendida");
        Assert.True(suspended.Suspend().IsSuccess);
        var viewerMember = ActiveMember(viewer);
        var colleagueMembers = colleagues.Select(ActiveMember).ToArray();
        var suspendedMember = ActiveMember(suspended);
        var invited = Member.Invite();
        var companies = new[] { "Empresa A", "Empresa B", "Empresa C", "Empresa D" }
            .Select(name => Company.Create(name, null, "UTC").Value).ToArray();
        var (a, b, c, d) = (companies[0], companies[1], companies[2], companies[3]);
        await WriteAsync(tenantId, context =>
        {
            context.Users.AddRange(colleagues.Append(viewer).Append(suspended));
            context.Members.AddRange(colleagueMembers.Append(viewerMember).Append(suspendedMember).Append(invited));
            context.Companies.AddRange(companies);
            context.CompanyMemberships.AddRange(companies.Select(company => CompanyMembership.Create(company.Id, viewerMember.Id).Value));
            // A: dos activos más quien mira; no cuentan la membresía inactiva ni la persona invitada.
            context.CompanyMemberships.Add(CompanyMembership.Create(a.Id, colleagueMembers[0].Id).Value);
            context.CompanyMemberships.Add(CompanyMembership.Create(a.Id, colleagueMembers[1].Id).Value);
            context.CompanyMemberships.Add(CompanyMembership.Create(a.Id, invited.Id).Value);
            var inactive = CompanyMembership.Create(a.Id, colleagueMembers[2].Id).Value;
            inactive.SetStatus(CompanyMembershipStatus.Inactive);
            context.CompanyMemberships.Add(inactive);
            // B: la cuenta suspendida no es un miembro activo.
            context.CompanyMemberships.Add(CompanyMembership.Create(b.Id, suspendedMember.Id).Value);
        });

        var sql = new QueryCounter();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(sql))));
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var builder = services.GetRequiredService<MemberHomeCompaniesBuilder>();
        sql.Target = services.GetRequiredService<ApplicationDbContext>();

        sql.Reset();
        var one = await builder.ListAsync(viewer.Id, Granted(withB: false, withC: false), Ct);
        var oneCompanyQueries = sql.Count;
        // C sin permiso para ver miembros y D sin permisos: la tarjeta de C no cuenta y D no aparece.
        sql.Reset();
        var cards = await builder.ListAsync(viewer.Id, Granted(withB: true, withC: true), Ct);
        var threeCompaniesQueries = sql.Count;

        Assert.Equal(oneCompanyQueries, threeCompaniesQueries);
        var onlyA = Assert.Single(one);
        Assert.Equal(a.Id, onlyA.CompanyId);
        Assert.Equal(3, onlyA.MemberCount);
        (Guid, int?)[] expected = [(a.Id, 3), (b.Id, 1), (c.Id, null)];
        Assert.Equal(expected, cards.Select(card => (card.CompanyId, card.MemberCount)));
        Assert.DoesNotContain(cards, card => card.CompanyId == d.Id);
        var memberships = services.GetRequiredService<ICompanyMembershipReader>();
        foreach (var card in cards.Where(card => card.MemberCount is not null))
            Assert.Equal((await memberships.GetFilterCountsAsync(card.CompanyId, new ListCompanyMembersRequest(), Ct)).Active,
                card.MemberCount);

        // A y B dejan ver los miembros; C tiene otro permiso en la empresa.
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> Granted(bool withB, bool withC)
        {
            var granted = new Dictionary<Guid, IReadOnlyList<string>> { [a.Id] = [Permissions.Company.Members.Read] };
            if (withB) granted[b.Id] = [Permissions.Company.Members.Read];
            if (withC) granted[c.Id] = [Permissions.Assistant.Use];
            return granted;
        }
    }

    private static ApplicationUser User(string name) => ApplicationUser.Create(name, "es-AR", "UTC", NowUtc).Value;

    private static Member ActiveMember(ApplicationUser user)
    {
        var member = Member.Invite(user.Id);
        Assert.True(member.Activate(NowUtc).IsSuccess);
        return member;
    }

    private async Task<Guid> OrganizationAsync()
    {
        var tenant = Tenant.CreateBusiness("Inicio miembro", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().Tenants.Add(tenant);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant.Id;
    }

    private async Task WriteAsync(Guid tenantId, Action<ApplicationDbContext> write)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            write(services.GetRequiredService<ApplicationDbContext>());
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }
}
