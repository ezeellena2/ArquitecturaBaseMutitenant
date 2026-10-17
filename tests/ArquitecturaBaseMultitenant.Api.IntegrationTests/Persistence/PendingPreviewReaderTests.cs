using System.Data.Common;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class PendingPreviewReaderTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_thousand_expired_invitations_keep_the_oldest_ten_and_total_with_a_fixed_query_count()
    {
        var (tenantId, inviterId) = await OrganizationAsync();
        var invitations = Enumerable.Range(0, 1_000).Select(index => InvitationFor(inviterId,
            NowUtc.AddDays(-2).AddMinutes(index / 2))).ToArray();
        await WriteAsync(tenantId, context =>
        {
            foreach (var (member, invitation) in invitations)
            {
                context.Members.Add(member);
                context.Invitations.Add(invitation);
            }
        });

        var sql = new QueryProbe();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(sql))));
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var reader = services.GetRequiredService<IOrganizationUserReader>();
        sql.ContextId = services.GetRequiredService<ApplicationDbContext>().ContextId;
        sql.Reset();
        var page = await reader.ListPendingAsync(new() { State = OrganizationUserState.Expired, Page = 8, PageSize = 100 }, NowUtc, Ct);
        var countForThousand = sql.Queries.Count;
        Assert.Equal(1_000, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(invitations.OrderBy(row => row.Invitation.ExpiresAtUtc).ThenBy(row => row.Member.Id)
            .Take(10).Select(row => row.Member.Id), page.Items.Select(row => row.MemberId));
        Assert.Contains(sql.Queries, query => query.Contains("LIMIT", StringComparison.Ordinal));

        // El mismo recorrido, ahora con una sola fila: ninguna consulta nueva depende de la cantidad de pendientes.
        sql.Reset();
        var one = await reader.ListPendingAsync(new() { State = OrganizationUserState.Expired,
            Search = invitations[999].Invitation.Destination.DisplayValue }, NowUtc, Ct);
        Assert.Single(one.Items);
        Assert.Equal(1, one.TotalCount);
        Assert.Equal(countForThousand, sql.Queries.Count);
        Assert.Equal(8, countForThousand);
    }

    [Fact]
    public async Task Company_invitation_policy_is_applied_before_counting_and_limiting()
    {
        var (tenantId, inviterId) = await OrganizationAsync();
        var north = Company.Create("Norte", null, "UTC").Value;
        var south = Company.Create("Sur", null, "UTC").Value;
        var role = Role.Create("Lectura", null, RoleScope.Organization, null, [Permissions.Users.Read]).Value;
        var allowed = Enumerable.Range(0, 12).Select(_ => InvitationFor(inviterId, NowUtc.AddDays(-1))).ToArray();
        var denied = Enumerable.Range(0, 15).Select(_ => InvitationFor(inviterId, NowUtc.AddDays(-3))).ToArray();
        await WriteAsync(tenantId, context =>
        {
            context.Companies.AddRange(north, south);
            context.Roles.Add(role);
            foreach (var row in allowed.Concat(denied))
            {
                context.Members.Add(row.Member);
                context.Invitations.Add(row.Invitation);
                var membership = CompanyMembership.Create(north.Id, row.Member.Id).Value;
                if (Array.IndexOf(denied, row) is >= 10) membership.SetStatus(CompanyMembershipStatus.Inactive);
                context.CompanyMemberships.Add(membership);
                if (Array.IndexOf(denied, row) is >= 0 and < 5)
                    context.RoleAssignments.Add(RoleAssignment.Assign(row.Member.Id, role.Id, role.Scope, null, null).Value);
                if (Array.IndexOf(denied, row) is >= 5 and < 10)
                    context.CompanyMemberships.Add(CompanyMembership.Create(south.Id, row.Member.Id).Value);
            }
            // Una membresía inactiva en otra empresa no invalida una invitación exclusiva de Norte.
            var inactiveElsewhere = CompanyMembership.Create(south.Id, allowed[0].Member.Id).Value;
            inactiveElsewhere.SetStatus(CompanyMembershipStatus.Inactive);
            context.CompanyMemberships.Add(inactiveElsewhere);
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var reader = services.GetRequiredService<IOrganizationUserReader>();
        var page = await reader.ListPendingAsync(new() { State = OrganizationUserState.Expired,
            CompanyId = north.Id, OnlyCompanyInvitations = true }, NowUtc, Ct);

        Assert.Equal(12, page.TotalCount);
        Assert.Equal(allowed.OrderBy(row => row.Member.Id).Take(10).Select(row => row.Member.Id),
            page.Items.Select(row => row.MemberId));
        Assert.All(page.Items, row => Assert.DoesNotContain(denied, item => item.Member.Id == row.MemberId));
        // El listado habitual conserva su contrato: incluye invitaciones compartidas y membresías inactivas.
        var general = await reader.ListAsync(new() { State = OrganizationUserState.Expired, CompanyId = north.Id }, NowUtc, Ct);
        Assert.Equal(27, general.TotalCount);

        var expired = await services.GetRequiredService<ICompanyMembershipReader>().ListAsync(north.Id,
            new ListCompanyMembersRequest { State = CompanyMemberState.Expired, OnlyCompanyInvitations = true }, Ct);
        Assert.Equal(12, expired.TotalCount);
        Assert.Equal(10, expired.Items.Count);
        Assert.All(expired.Items, row =>
        {
            Assert.Equal(CompanyMemberState.Invited, row.State);
            Assert.True(row.InvitationOnlyForCompany);
            Assert.NotNull(row.InvitationId);
        });
    }

    [Fact]
    public async Task Domain_preview_counts_only_pending_and_keeps_a_stable_oldest_ten()
    {
        var (tenantId, _) = await OrganizationAsync();
        var domains = Enumerable.Range(0, 1_000).Select(index => TenantDomain.Create(tenantId,
            OrganizationEmailDomain.Create($"d-{Guid.NewGuid():N}.example").Value, "synthetic",
            NowUtc.AddDays(-2).AddMinutes(index / 2)).Value).ToArray();
        var verified = TenantDomain.Create(tenantId, OrganizationEmailDomain.Create($"verified-{Guid.NewGuid():N}.example").Value,
            "synthetic", NowUtc.AddDays(-4)).Value;
        var removed = TenantDomain.Create(tenantId, OrganizationEmailDomain.Create($"removed-{Guid.NewGuid():N}.example").Value,
            "synthetic", NowUtc.AddDays(-4)).Value;
        Assert.True(verified.Verify(NowUtc).IsSuccess);
        Assert.True(removed.Remove().IsSuccess);
        await WriteAsync(tenantId, context => context.TenantDomains.AddRange(domains.Concat([verified, removed])));

        var sql = new QueryProbe();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(sql))));
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var reader = services.GetRequiredService<ITenantDomainReader>();
        sql.ContextId = services.GetRequiredService<ApplicationDbContext>().ContextId;
        sql.Reset();
        var page = await reader.ListPendingAsync(Ct);

        Assert.Equal(1_000, page.TotalCount);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(domains.OrderBy(domain => domain.CreatedAtUtc).ThenBy(domain => domain.Id).Take(10)
            .Select(domain => domain.Id), page.Items.Select(domain => domain.Id));
        Assert.All(page.Items, domain => Assert.Equal(TenantDomainStatus.Pending, domain.Status));
        Assert.Equal(2, sql.Queries.Count);
        Assert.Contains(sql.Queries, query => query.Contains("LIMIT", StringComparison.Ordinal));

        sql.Reset();
        Assert.Equal(verified.Domain, await reader.FindHomeDomainAsync(Ct));
        Assert.Single(sql.Queries);
        Assert.Contains("LIMIT", sql.Queries[0], StringComparison.Ordinal);

        var verifiedPage = await reader.ListPagedAsync(new ListOrganizationDomainsRequest
            { Status = TenantDomainStatus.Verified }, Ct);
        Assert.Equal(1, verifiedPage.TotalCount);
        Assert.Equal(verified.Id, Assert.Single(verifiedPage.Items).Id);
        var pendingPage = await reader.ListPagedAsync(new ListOrganizationDomainsRequest
            { Status = TenantDomainStatus.Pending }, Ct);
        Assert.Equal(1_000, pendingPage.TotalCount);
        Assert.All(pendingPage.Items, domain => Assert.Equal(TenantDomainStatus.Pending, domain.Status));
    }

    [Fact]
    public async Task Pending_deletions_use_the_requested_instant_instead_of_member_creation()
    {
        var (tenantId, _) = await OrganizationAsync();
        var rows = Enumerable.Range(0, 15).Select(index =>
        {
            var user = ApplicationUser.Create($"Persona {index}", "es-AR", "UTC", NowUtc.AddDays(-30)).Value;
            Assert.True(user.RequestDeletion("Synthetic", NowUtc.AddMinutes(-index), 30).IsSuccess);
            var member = Member.Invite(user.Id);
            Assert.True(member.Activate(NowUtc.AddDays(-30)).IsSuccess);
            return (User: user, Member: member);
        }).ToArray();
        await WriteAsync(tenantId, context =>
        {
            context.Users.AddRange(rows.Select(row => row.User));
            context.Members.AddRange(rows.Select(row => row.Member));
        });
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var page = await services.GetRequiredService<IOrganizationUserReader>().ListPendingAsync(new()
            { State = OrganizationUserState.PendingDeletion }, NowUtc, Ct);

        Assert.Equal(15, page.TotalCount);
        Assert.Equal(rows.OrderBy(row => row.User.DeletionRequestedAtUtc).ThenBy(row => row.Member.Id).Take(10)
            .Select(row => row.Member.Id), page.Items.Select(row => row.MemberId));
    }

    private async Task<(Guid TenantId, Guid InviterId)> OrganizationAsync()
    {
        var tenant = Tenant.CreateBusiness("Preview", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        var inviter = ApplicationUser.Create("Invitante", "es-AR", "UTC", NowUtc).Value;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            context.Tenants.Add(tenant);
            context.Users.Add(inviter);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return (tenant.Id, inviter.Id);
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

    private static (Member Member, Invitation Invitation) InvitationFor(Guid inviterId, DateTime expiresUtc)
    {
        var member = Member.Invite();
        return (member, Invitation.Issue(member.Id, inviterId, Email.Create($"preview-{member.Id:N}@example.test").Value,
            "Email", member.Id.ToString("N"), expiresUtc.AddDays(-1), TimeSpan.FromDays(1)));
    }

    private sealed class QueryProbe : DbCommandInterceptor
    {
        public List<string> Queries { get; } = [];
        public DbContextId? ContextId { get; set; }
        public void Reset() => Queries.Clear();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ContextId == ContextId) Queries.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
