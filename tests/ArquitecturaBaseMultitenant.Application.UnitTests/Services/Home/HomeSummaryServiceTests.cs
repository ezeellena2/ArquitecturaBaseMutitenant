using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Services.Home;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

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
            Assert.Equal(group.Items, result.Value.Pending);
        }
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
            Task.FromResult<IReadOnlyList<string>>([Permissions.Users.Invite]);
        public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId, CancellationToken ct)
        {
            OrganizationReads++;
            return Task.FromResult<IReadOnlyList<string>>(revoked && OrganizationReads > 1 ? [] : [Permissions.Users.Invite]);
        }
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId,
            CancellationToken ct) => Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(new Dictionary<Guid, IReadOnlyList<string>>());
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
        public Task InvalidateRoleAsync(Guid roleId, CancellationToken ct) => Task.CompletedTask;
    }
}
