using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Validation.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Roles;

public sealed class RoleServiceWriteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Invalid_create_does_not_open_a_transaction()
    {
        using var fixture = new ServiceFixture<RoleService>(new CreateRoleRequestValidator());
        var unitOfWork = new FakeUnitOfWork();
        var service = new RoleService(new RoleServiceTests.FakeRoleReader(),
            new RoleServiceTests.FakeRoleRepository(), new RoleServiceTests.FakePermissionService(),
            fixture.Validator, unitOfWork, fixture.TimeProvider, fixture.Logger);

        var result = await service.CreateAsync(new CreateRoleRequest(null, null, null, null, null), Ct);

        Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(0, unitOfWork.Transactions);
    }

    [Fact]
    public async Task A_system_role_cannot_be_deleted()
    {
        var role = SystemRoles.CreateTenantAdmin();
        var repository = new RecordingRoleRepository(role);
        var unitOfWork = new FakeUnitOfWork();
        using var fixture = new ServiceFixture<RoleService>(new DeleteRoleRequestValidator());
        var service = new RoleService(new RoleServiceTests.FakeRoleReader(), repository,
            new RoleServiceTests.FakePermissionService(), fixture.Validator, unitOfWork,
            fixture.TimeProvider, fixture.Logger);

        var result = await service.DeleteAsync(new DeleteRoleRequest(role.Id, 0), Ct);

        Assert.Equal(RoleErrors.SystemRoleCannotChange, result.Error);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.False(repository.WasRemoved);
    }

    [Fact]
    public async Task A_role_with_assigned_users_cannot_change_where_it_applies()
    {
        var north = Guid.CreateVersion7();
        var role = Role.Create("Lectores", null, RoleScope.SpecificCompany, north,
            [Permissions.Company.Members.Read]).Value;
        var repository = new RecordingRoleRepository(role);
        using var fixture = new ServiceFixture<RoleService>(new UpdateRoleRequestValidator());
        var service = new RoleService(new RoleServiceTests.FakeRoleReader { UserCount = 2 }, repository,
            new RoleServiceTests.FakePermissionService(), fixture.Validator, new FakeUnitOfWork(),
            fixture.TimeProvider, fixture.Logger);

        var moved = await service.UpdateAsync(new UpdateRoleRequest(role.Id, "Lectores", null,
            RoleScope.SpecificCompany, Guid.CreateVersion7(), [Permissions.Company.Members.Read], 0), Ct);
        var everywhere = await service.UpdateAsync(new UpdateRoleRequest(role.Id, "Lectores", null,
            RoleScope.AnyCompany, null, [Permissions.Company.Members.Read], 0), Ct);
        var renamed = await service.UpdateAsync(new UpdateRoleRequest(role.Id, "Lectores Norte", null,
            RoleScope.SpecificCompany, north, [Permissions.Company.Members.Read], 0), Ct);

        Assert.Equal(RoleErrors.ScopeChangeHasUsersCode, moved.Error.Code);
        Assert.Equal(2, moved.Error.Metadata![RoleErrors.UserCountKey]);
        Assert.Equal(RoleErrors.ScopeChangeHasUsersCode, everywhere.Error.Code);
        Assert.True(renamed.IsSuccess);
        Assert.Equal(north, role.CompanyId);
        Assert.Equal("Lectores Norte", role.Name);
    }

    [Fact]
    public async Task A_role_without_assigned_users_changes_where_it_applies()
    {
        var role = Role.Create("Lectores", null, RoleScope.SpecificCompany, Guid.CreateVersion7(),
            [Permissions.Company.Members.Read]).Value;
        using var fixture = new ServiceFixture<RoleService>(new UpdateRoleRequestValidator());
        var service = new RoleService(new RoleServiceTests.FakeRoleReader(), new RecordingRoleRepository(role),
            new RoleServiceTests.FakePermissionService(), fixture.Validator, new FakeUnitOfWork(),
            fixture.TimeProvider, fixture.Logger);

        var result = await service.UpdateAsync(new UpdateRoleRequest(role.Id, "Lectores", null,
            RoleScope.AnyCompany, null, [Permissions.Company.Members.Read], 0), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(RoleScope.AnyCompany, role.Scope);
        Assert.Null(role.CompanyId);
    }

    private sealed class RecordingRoleRepository(Role role) : IRoleRepository
    {
        public bool WasRemoved { get; private set; }
        public Task<Role?> GetByIdAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken) =>
            Task.FromResult<Role?>(roleId == role.Id ? role : null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult<Role?>(null);
        public void Add(Role item) { }
        public void MarkUpdated(Role item) { }
        public void Remove(Role item) => WasRemoved = true;
    }
}
