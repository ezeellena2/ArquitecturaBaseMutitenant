using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Validation.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;

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
    }

    private static RoleService Create(FakeRoleReader reader, ServiceFixture<RoleService> fixture) =>
        new(reader, new FakeRoleRepository(), new FakePermissionService(), fixture.Validator,
            new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

    internal sealed class FakeRoleReader : IRoleReader
    {
        public PagedResult<RoleRow> Page { get; init; } = new([], 1, 10, 0);
        public ListRolesRequest? PageRequest { get; private set; }
        public RoleRow? Role { get; init; }
        public bool NameExists { get; init; }
        public int UserCount { get; init; }

        public Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RoleRow>>([]);
        public Task<PagedResult<RoleRow>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken)
        {
            PageRequest = request;
            return Task.FromResult(Page);
        }
        public Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(Role);
        public Task<bool> ExistsByNameAsync(string name, Guid? excludedRoleId, CancellationToken cancellationToken) =>
            Task.FromResult(NameExists);
        public Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(UserCount);
        public Task<IReadOnlyList<Guid>> ListRoleIdsForUserAsync(Guid userId, Guid? companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyList<string>> ListPermissionsForRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    internal sealed class FakeRoleRepository : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult<Role?>(null);
        public void Add(Role role) { }
        public void MarkUpdated(Role role) { }
        public void Remove(Role role) { }
    }

    internal sealed class FakePermissionService : IPermissionService
    {
        public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
