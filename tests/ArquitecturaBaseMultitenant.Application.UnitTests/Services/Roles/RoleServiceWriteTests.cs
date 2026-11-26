using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.UnitTests.Services.Companies;
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
            new RoleServiceTests.FakeRoleRepository(),
            new CompanyServiceTests.FakeCompanyReader(), RoleServiceTests.OwnerHeld(),
            RoleServiceTests.OwnerDelegation(),
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
            new CompanyServiceTests.FakeCompanyReader(), RoleServiceTests.OwnerHeld(),
            RoleServiceTests.OwnerDelegation(), fixture.Validator,
            unitOfWork, fixture.TimeProvider, fixture.Logger);

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
            new CompanyServiceTests.FakeCompanyReader(), RoleServiceTests.OwnerHeld(),
            RoleServiceTests.OwnerDelegation(), fixture.Validator,
            new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

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
            new CompanyServiceTests.FakeCompanyReader(), RoleServiceTests.OwnerHeld(),
            RoleServiceTests.OwnerDelegation(), fixture.Validator,
            new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

        var result = await service.UpdateAsync(new UpdateRoleRequest(role.Id, "Lectores", null,
            RoleScope.AnyCompany, null, [Permissions.Company.Members.Read], 0), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(RoleScope.AnyCompany, role.Scope);
        Assert.Null(role.CompanyId);
    }

    // 260: guardar un rol guarda lo pedido más lo que incluye, en la organización y en una empresa.
    [Fact]
    public async Task Saving_a_role_keeps_what_was_asked_plus_what_it_includes()
    {
        var role = Role.Create("Equipo", null, RoleScope.Organization, null, [Permissions.Audit.Read]).Value;
        var repository = new RecordingRoleRepository(role);
        using var createFixture = new ServiceFixture<RoleService>(new CreateRoleRequestValidator());
        var creating = new RoleService(new RoleServiceTests.FakeRoleReader(), repository,
            new CompanyServiceTests.FakeCompanyReader(), RoleServiceTests.OwnerHeld(),
            RoleServiceTests.OwnerDelegation(), createFixture.Validator, new FakeUnitOfWork(),
            createFixture.TimeProvider, createFixture.Logger);
        using var updateFixture = new ServiceFixture<RoleService>(new UpdateRoleRequestValidator());
        var updating = new RoleService(new RoleServiceTests.FakeRoleReader(), repository,
            new CompanyServiceTests.FakeCompanyReader(), RoleServiceTests.OwnerHeld(),
            RoleServiceTests.OwnerDelegation(), updateFixture.Validator, new FakeUnitOfWork(),
            updateFixture.TimeProvider, updateFixture.Logger);

        var created = await creating.CreateAsync(new CreateRoleRequest("Personas", null, RoleScope.Organization, null,
            [Permissions.Users.Manage, Permissions.Roles.Assign]), Ct);
        Assert.True(created.IsSuccess);
        Assert.Equal([Permissions.Roles.Assign, Permissions.Roles.Read, Permissions.Users.Manage, Permissions.Users.Read],
            repository.Added!.Permissions);
        var company = await creating.CreateAsync(new CreateRoleRequest("Miembros", null, RoleScope.AnyCompany, null,
            [Permissions.Company.Members.Manage]), Ct);
        Assert.True(company.IsSuccess);
        Assert.Equal([Permissions.Company.Members.Manage, Permissions.Company.Members.Read], repository.Added!.Permissions);

        var updated = await updating.UpdateAsync(new UpdateRoleRequest(role.Id, "Equipo", null, RoleScope.Organization,
            null, [Permissions.Settings.Manage], 0), Ct);
        Assert.True(updated.IsSuccess);
        Assert.Equal([Permissions.Settings.Manage, Permissions.Settings.Read], role.Permissions);
    }

    // ADR 0061: con solo «Administrar roles» nadie suma ni saca un permiso que no tiene; el nombre no cuenta, y un rol de
    // sistema sigue respondiendo que no cambia.
    [Fact]
    public async Task Nobody_gives_or_takes_away_a_permission_they_do_not_have()
    {
        var role = Role.Create("Equipo", null, RoleScope.Organization, null,
            [Permissions.Roles.Read, Permissions.Settings.Read]).Value;
        var repository = new RecordingRoleRepository(role);
        var reader = new RoleServiceTests.FakeRoleReader { Held = [Permissions.Roles.Manage] };
        using var createFixture = new ServiceFixture<RoleService>(new CreateRoleRequestValidator());
        var creating = new RoleService(reader, repository, new CompanyServiceTests.FakeCompanyReader(),
            RoleServiceTests.Held(reader), RoleServiceTests.Delegation(reader), createFixture.Validator,
            new FakeUnitOfWork(), createFixture.TimeProvider, createFixture.Logger);
        using var updateFixture = new ServiceFixture<RoleService>(new UpdateRoleRequestValidator());
        var updating = new RoleService(reader, repository, new CompanyServiceTests.FakeCompanyReader(),
            RoleServiceTests.Held(reader), RoleServiceTests.Delegation(reader), updateFixture.Validator,
            new FakeUnitOfWork(), updateFixture.TimeProvider, updateFixture.Logger);

        var foreign = await creating.CreateAsync(new CreateRoleRequest("Ajustes", null, RoleScope.Organization, null,
            [Permissions.Settings.Manage]), Ct);
        var own = await creating.CreateAsync(new CreateRoleRequest("Lectores", null, RoleScope.Organization, null,
            [Permissions.Roles.Read]), Ct);
        var takesAway = await updating.UpdateAsync(new UpdateRoleRequest(role.Id, "Equipo", null, RoleScope.Organization,
            null, [Permissions.Roles.Read], 0), Ct);
        var renamed = await updating.UpdateAsync(new UpdateRoleRequest(role.Id, "Equipo B", null, RoleScope.Organization,
            null, [Permissions.Roles.Read, Permissions.Settings.Read], 0), Ct);
        var system = SystemRoles.CreateTenantAdmin();
        var systemUpdate = await new RoleService(reader, new RecordingRoleRepository(system),
                new CompanyServiceTests.FakeCompanyReader(),
                RoleServiceTests.Held(reader), RoleServiceTests.Delegation(reader), updateFixture.Validator,
                new FakeUnitOfWork(), updateFixture.TimeProvider, updateFixture.Logger)
            .UpdateAsync(new UpdateRoleRequest(system.Id, "Otro", null, RoleScope.Organization, null,
                [Permissions.Roles.Read], 0), Ct);

        Assert.Equal(RoleErrors.PermissionsNotHeld, foreign.Error);
        Assert.True(own.IsSuccess);
        Assert.Equal(RoleErrors.PermissionsNotHeld, takesAway.Error);
        Assert.True(renamed.IsSuccess);
        Assert.Equal("Equipo B", role.Name);
        Assert.Equal(RoleErrors.SystemRoleCannotChange, systemUpdate.Error);
    }

    private sealed class RecordingRoleRepository(Role role) : IRoleRepository
    {
        public bool WasRemoved { get; private set; }
        public Role? Added { get; private set; }
        public Task<Role?> GetByIdAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken) =>
            Task.FromResult<Role?>(roleId == role.Id ? role : null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult<Role?>(null);
        public void Add(Role item) => Added = item;
        public void MarkUpdated(Role item) { }
        public void Remove(Role item) => WasRemoved = true;
    }
}
