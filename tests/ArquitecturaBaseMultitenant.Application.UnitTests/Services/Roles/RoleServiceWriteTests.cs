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
