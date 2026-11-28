using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.UnitTests.Services.Companies;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Validation.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Roles;

public sealed class RoleServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_validates_and_maps_rows_without_losing_page_metadata()
    {
        var id = Guid.CreateVersion7();
        var reader = new FakeRoleReader
        {
            Page = new PagedResult<RoleRow>(
                [new(id, "Lectores", "Solo lectura", RoleScope.Organization, null, false, 2,
                    [Permissions.Users.Read], 7, new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc))],
                2, 10, 14),
        };
        using var fixture = new ServiceFixture<RoleService>(new ListRolesRequestValidator());
        var service = Create(reader, fixture);

        var result = await service.ListAsync(new ListRolesRequest { Page = 2, Search = "lect" }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal((2, 10, 14, 2), (result.Value.Page, result.Value.PageSize,
            result.Value.TotalCount, result.Value.TotalPages));
        Assert.Equal(id, Assert.Single(result.Value.Items).Id);
        Assert.Equal("lect", reader.PageRequest?.Search);
    }

    [Fact]
    public async Task A_missing_role_is_not_found()
    {
        using var fixture = new ServiceFixture<RoleService>();
        var service = Create(new FakeRoleReader(), fixture);

        var result = await service.GetAsync(Guid.CreateVersion7(), Ct);

        Assert.Equal(RoleErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Permission_groups_follow_catalog_order_and_translate_the_names()
    {
        using var fixture = new ServiceFixture<RoleService>();
        var service = Create(new FakeRoleReader(), fixture);

        var result = await service.GetPermissionsAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(Permissions.All, result.Value.SelectMany(group => group.Permissions)
            .Select(permission => permission.Code));
        Assert.Equal("Usuarios", result.Value[0].Name);
        Assert.Contains(result.Value.SelectMany(group => group.Permissions),
            permission => permission.Code == Permissions.Roles.Manage && permission.Name == "Administrar roles");
        // 260: cada permiso trae lo que incluye, de su misma área.
        var items = result.Value.SelectMany(group => group.Permissions).ToArray();
        Assert.All(items, item => Assert.Equal(Permissions.Inclusions.Of(item.Code), item.Includes));
        Assert.Equal([Permissions.Users.Read], items.Single(item => item.Code == Permissions.Users.Manage).Includes);
        Assert.Empty(items.Single(item => item.Code == Permissions.Users.Read).Includes);
        // El Dueño puede dar todo el catálogo.
        Assert.All(items, item => Assert.True(item.Grantable));
    }

    // 269 y ADR 0061: grantable dice si quien pide lo tiene, con lo que incluye; un rol, si lo puede dar ahí.
    [Fact]
    public async Task Grantable_says_what_the_requester_holds_in_permissions_and_roles()
    {
        var companyId = Guid.CreateVersion7();
        var created = new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
        var reader = new FakeRoleReader
        {
            Held = [Permissions.Users.Manage, Permissions.Roles.Assign],
            Page = new PagedResult<RoleRow>(
            [
                new(Guid.CreateVersion7(), "Lectores", null, RoleScope.Organization, null, false, 0,
                    [Permissions.Users.Read, Permissions.Roles.Read], 1, created),
                new(Guid.CreateVersion7(), "Configuración", null, RoleScope.Organization, null, false, 0,
                    [Permissions.Settings.Manage, Permissions.Settings.Read], 1, created),
                new(Guid.CreateVersion7(), "Miembros", null, RoleScope.SpecificCompany, companyId, false, 0,
                    [Permissions.Company.Members.Read], 1, created),
            ], 1, 10, 3),
        };
        using var fixture = new ServiceFixture<RoleService>(new ListRolesRequestValidator());
        var service = new RoleService(reader, new FakeRoleRepository(), new CompanyServiceTests.FakeCompanyReader(),
            Held(reader), Delegation(reader), fixture.Validator, new FakeUnitOfWork(),
            fixture.TimeProvider, fixture.Logger);

        var permissions = (await service.GetPermissionsAsync(Ct)).Value.SelectMany(group => group.Permissions)
            .ToDictionary(item => item.Code, item => item.Grantable);
        var roles = (await service.ListAsync(new ListRolesRequest(), Ct)).Value.Items;

        Assert.Equal([Permissions.Users.Read, Permissions.Users.Manage, Permissions.Roles.Read, Permissions.Roles.Assign],
            permissions.Where(item => item.Value).Select(item => item.Key));
        Assert.Equal([true, false, false], roles.Select(role => role.Grantable));
    }

    private static RoleService Create(FakeRoleReader reader, ServiceFixture<RoleService> fixture) =>
        new(reader, new FakeRoleRepository(), new CompanyServiceTests.FakeCompanyReader(), OwnerHeld(), OwnerDelegation(),
            fixture.Validator, new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

    /// <summary>Lo que tiene quien pide, leído de <paramref name="reader"/> (B3 de Acciones).</summary>
    internal static DelegationPermissionsResolver Held(IRoleReader reader) => new(new SignedInUser(), reader);

    /// <summary>La guarda de la 0061, con lo que tiene quien pide leído de <paramref name="reader"/>.</summary>
    internal static OrganizationDelegationGuard Delegation(IRoleReader reader) =>
        new(new SignedInUser(), Held(reader), reader);

    /// <summary>Quien pide como Dueño: tiene todo el catálogo de la organización.</summary>
    internal static DelegationPermissionsResolver OwnerHeld() => Held(new FakeRoleReader());

    /// <summary>Una guarda con quien pide como Dueño: tiene todo el catálogo de la organización.</summary>
    internal static OrganizationDelegationGuard OwnerDelegation() => Delegation(new FakeRoleReader());

    internal sealed class SignedInUser : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.CreateVersion7();
        public Access? Access => Domain.Users.Access.Business;
    }

    internal sealed class FakeRoleReader : IRoleReader
    {
        public Task<RoleFilterCountsResponse> CountFiltersAsync(string? search, CancellationToken cancellationToken) =>
            Task.FromResult(new RoleFilterCountsResponse(new(0, 0, 0), new(0, 0, 0), 0, new Dictionary<Guid, int>()));

        public PagedResult<RoleRow> Page { get; init; } = new([], 1, 10, 0);
        public ListRolesRequest? PageRequest { get; private set; }
        public RoleRow? Role { get; init; }
        public bool NameExists { get; init; }
        public int UserCount { get; init; }

        /// <summary>Lo que dan a quien pide sus roles de organización; por defecto, todo el catálogo, como el Dueño.</summary>
        public IReadOnlyList<string> Held { get; init; } = [.. Permissions.All];

        private readonly Guid heldRoleId = Guid.CreateVersion7();

        public Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RoleRow>>([]);
        public Task<PagedResult<RoleRow>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken)
        {
            PageRequest = request;
            return Task.FromResult(Page);
        }
        public Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(Role);
        public Task<IReadOnlyList<RoleRow>> ListByIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RoleRow>>(Role is null ? [] : [Role]);
        public Task<bool> ExistsByNameAsync(string name, Guid? companyId, Guid? excludedRoleId,
            CancellationToken cancellationToken) =>
            Task.FromResult(NameExists);
        public Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(UserCount);
        public Task<IReadOnlyList<Guid>> ListOrganizationRoleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([heldRoleId]);
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ListCompanyRoleIdsAsync(Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ListPermissionsByRoleAsync(
            IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(
                roleIds.Contains(heldRoleId)
                    ? new Dictionary<Guid, IReadOnlyList<string>> { [heldRoleId] = Held }
                    : new Dictionary<Guid, IReadOnlyList<string>>());
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ListActiveUsersOrganizationRoleIdsByRoleAsync(
            Guid roleId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());
    }

    internal sealed class FakeRoleRepository : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult<Role?>(null);
        public void Add(Role role) { }
        public void MarkUpdated(Role role) { }
        public void Remove(Role role) { }
    }
}
