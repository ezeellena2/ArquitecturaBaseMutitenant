using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Services.Home;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Home;

public sealed class HomeSummaryServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("removed")]
    [InlineData("organizationRole")]
    [InlineData("otherCompany")]
    public async Task A_company_preview_denies_memberships_changed_while_the_reader_hydrates_the_page(string change)
    {
        using var fixture = new ServiceFixture<HomeService>();
        var companyId = Guid.NewGuid();
        var nowUtc = fixture.TimeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<OrganizationUserCompanyRow> companies = change == "removed" ? [] :
            [new(companyId, "Norte", CompanyMembershipStatus.Active, [])];
        if (change == "otherCompany") companies = [.. companies, new(Guid.NewGuid(), "Sur", CompanyMembershipStatus.Active, [])];
        IReadOnlyList<OrganizationUserRoleRow> roles = change == "organizationRole"
            ? [new(Guid.NewGuid(), "Lectura", false, RoleScope.Organization, null)] : [];
        var row = new OrganizationUserRow(Guid.NewGuid(), null, "Invitada", null, false, OrganizationUserState.Expired,
            Guid.NewGuid(), nowUtc.AddDays(-2), nowUtc.AddDays(-1), roles, companies, 1, nowUtc.AddDays(-3), null);
        var reader = new ChangingCompanyReader(row);
        var source = new OrganizationUserPendingItems(reader, new PermissionsProbe(false), fixture.TimeProvider);
        var context = new PendingItemContext(Guid.NewGuid(), [], new Dictionary<Guid, IReadOnlyList<string>>
        { [companyId] = [Permissions.Company.Members.Manage] });

        var groups = await source.RevalidateGroupsAsync([new("users", "invitationExpired", 1,
            [new("users", "invitationExpired", null, false, nowUtc, null, row.MemberId, companyId, "Norte")],
            companyId, "Norte")], context, Ct);

        Assert.Empty(groups.SelectMany(group => group.Items));
        Assert.Equal(1, reader.Reads);
        Assert.True(reader.Request!.OnlyCompanyInvitations);
        Assert.Equal(PendingGroup.PreviewLimit, reader.Request.PageSize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Summary_revalidates_once_with_fresh_permissions_without_checking_every_item(bool revoked)
    {
        using var fixture = new ServiceFixture<HomeService>();
        var permissions = new PermissionsProbe(revoked);
        var source = new GroupSource();
        IHomeService service = new HomeService(new(null!, null!, null!, null!, null!, null!, fixture.TimeProvider),
            new(null!), [source], [], permissions, new CurrentUser(), fixture.TimeProvider, fixture.Logger);

        var result = await service.GetSummaryAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, permissions.OrganizationReads);
        Assert.Equal(1, source.ListCalls);
        Assert.Equal(1, source.RevalidationCalls);
        Assert.Equal(0, source.LegacyCalls);
        Assert.Equal(0, permissions.PerItemChecks);
        if (revoked)
        {
            Assert.Empty(result.Value.Pending);
            Assert.Empty(result.Value.PendingGroups!);
        }
        else
        {
            var group = Assert.Single(result.Value.PendingGroups!);
            Assert.Equal(1_000, group.TotalCount);
            Assert.Equal(10, group.Items.Count);
            // La lista plana anterior es el adaptador vacío del contrato (LegacyPendingLimit = 0).
            Assert.Empty(result.Value.Pending);
        }
    }

    /// <summary>
    /// Revisión de la base de la 12: el inicio lee cada proyección de permisos una vez para armar el contexto y otra para
    /// revalidar lo que trajeron las fuentes, y el inicio plano (el del Asistente) una sola vez. Las pedía por separado y
    /// leía la de la organización hasta cinco veces por pedido.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Home_reads_each_permission_projection_once_per_phase(bool organizationRole)
    {
        using var fixture = new ServiceFixture<HomeService>();
        IReadOnlyList<string> organization = organizationRole ? [Permissions.Users.Invite] : [];
        var companies = new Dictionary<Guid, IReadOnlyList<string>> { [Guid.NewGuid()] = [Permissions.Company.Members.Manage] };
        var summaryCache = new CountingPermissionCache(organization, companies);
        var homeCache = new CountingPermissionCache(organization, companies);

        var summary = await Home(fixture, summaryCache).GetSummaryAsync(Ct);
        var home = await Home(fixture, homeCache).GetAsync(Ct);

        Assert.True(summary.IsSuccess);
        Assert.True(home.IsSuccess);
        Assert.Equal((2, 2), (summaryCache.OrganizationReads, summaryCache.CompanyReads));
        Assert.Equal((1, 1), (homeCache.OrganizationReads, homeCache.CompanyReads));
        // Lo que ve no cambia: con un rol de organización, Inicio-Org con su pendiente; sin él, Inicio-Miembro sin ese pendiente.
        Assert.Equal(organizationRole, summary.Value.Organization is not null);
        Assert.Equal(organizationRole ? 1 : 0, summary.Value.PendingGroups!.Count);
    }

    private static HomeService Home(ServiceFixture<HomeService> fixture, CountingPermissionCache cache) => new(
        new(null!, null!, null!, null!, null!, null!, fixture.TimeProvider), new(new NoMemberships()), [new GroupSource()], [],
        new PermissionService(cache, new FakeTenantContext { TenantId = Guid.NewGuid(), TenantKind = TenantKind.Business },
            new CompanyActivityReader(), fixture.TimeProvider, NullLogger<PermissionService>.Instance),
        new CurrentUser(), fixture.TimeProvider, fixture.Logger);

    /// <summary>Inicio-Miembro sin empresas propias: «Tus empresas» queda vacía.</summary>
    private sealed class NoMemberships : ICompanyMembershipReader
    {
        public Task<IReadOnlyList<UserCompanyRow>> ListActiveForUserAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UserCompanyRow>>([]);
        public Task<PagedResult<CompanyMemberRow>> ListAsync(Guid companyId, ListCompanyMembersRequest request,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<CompanyMemberFilterCountsResponse> GetFilterCountsAsync(Guid companyId, ListCompanyMembersRequest request,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, int>> CountActiveMembersAsync(IReadOnlyCollection<Guid> companyIds,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<CompanyCandidateRow>> ListCandidatesAsync(Guid companyId, ListCompanyCandidatesRequest request,
            CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CurrentUser : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Access? Access => ArquitecturaBaseMultitenant.Domain.Users.Access.Business;
    }

    private sealed class ChangingCompanyReader(OrganizationUserRow row) : IOrganizationUserReader
    {
        public int Reads { get; private set; }
        public ListOrganizationUsersRequest? Request { get; private set; }
        public Task<PagedResult<OrganizationUserRow>> ListAsync(ListOrganizationUsersRequest request, DateTime nowUtc, CancellationToken ct)
        {
            Reads++;
            Request = request;
            return Task.FromResult(new PagedResult<OrganizationUserRow>([row], 1, request.Page, request.PageSize));
        }
        public Task<OrganizationUserFilterCountsResponse> GetFilterCountsAsync(ListOrganizationUsersRequest request,
            DateTime nowUtc, CancellationToken ct) => throw new InvalidOperationException("Unexpected counts read");
        public Task<OrganizationUserRow?> FindByIdAsync(Guid memberId, DateTime nowUtc, CancellationToken ct) =>
            throw new InvalidOperationException("Unexpected per-item read");
    }

    private sealed class GroupSource : IPendingGroupSource
    {
        public int ListCalls { get; private set; }
        public int RevalidationCalls { get; private set; }
        public int LegacyCalls { get; private set; }
        public Task<IReadOnlyList<PendingGroup>> ListGroupsAsync(PendingItemContext context, CancellationToken ct)
        {
            ListCalls++;
            IReadOnlyList<PendingItem> items = Enumerable.Range(0, 10).Select(index => new PendingItem("users",
                "invitationExpired", index.ToString(System.Globalization.CultureInfo.InvariantCulture), false,
                null, null, Guid.NewGuid())).ToArray();
            return Task.FromResult<IReadOnlyList<PendingGroup>>([new("users", "invitationExpired", 1_000, items)]);
        }
        public Task<IReadOnlyList<PendingGroup>> RevalidateGroupsAsync(IReadOnlyList<PendingGroup> groups,
            PendingItemContext context, CancellationToken ct)
        {
            RevalidationCalls++;
            return Task.FromResult(context.Can(Permissions.Users.Invite) ? groups : []);
        }
        public Task<IReadOnlyList<PendingItem>> ListAsync(PendingItemContext context, CancellationToken ct)
        {
            LegacyCalls++;
            return Task.FromResult<IReadOnlyList<PendingItem>>([]);
        }
        public Task<bool> CanResolveAsync(PendingItem item, Guid userId, CancellationToken ct)
        {
            LegacyCalls++;
            return Task.FromResult(true);
        }
    }

    private sealed class PermissionsProbe(bool revoked) : IPermissionService
    {
        public int OrganizationReads { get; private set; }
        public int PerItemChecks { get; private set; }
        public Task<IReadOnlyList<string>> GetAssignedOrganizationPermissionsAsync(Guid userId, CancellationToken ct) =>
            throw new InvalidOperationException("Home reads the permission projections together.");
        public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId, CancellationToken ct) =>
            throw new InvalidOperationException("Home reads the permission projections together.");
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId,
            CancellationToken ct) => throw new InvalidOperationException("Home reads the permission projections together.");
        public Task<BusinessPermissions> GetBusinessPermissionsAsync(Guid userId, CancellationToken ct)
        {
            OrganizationReads++;
            IReadOnlyList<string> granted = revoked && OrganizationReads > 1 ? [] : [Permissions.Users.Invite];
            return Task.FromResult(new BusinessPermissions(granted, new Dictionary<Guid, IReadOnlyList<string>>(), granted));
        }
        public Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct)
        {
            PerItemChecks++;
            return Task.FromResult(true);
        }
        public Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission, CancellationToken ct)
        {
            PerItemChecks++;
            return Task.FromResult(true);
        }
        public Task<bool> HasCompanyRequirementAsync(Guid userId, Guid companyId, string permission,
            string? organizationPermission, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasCompanyPermissionAsync(BusinessPermissions granted, Guid companyId, string permission,
            CancellationToken ct)
        {
            PerItemChecks++;
            return Task.FromResult(true);
        }
    }
}
