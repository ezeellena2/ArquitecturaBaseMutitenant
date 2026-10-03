using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Roles;

public sealed class PlatformPermissionServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(PlatformRole.Owner)]
    [InlineData(PlatformRole.Support)]
    public async Task Active_operator_gets_only_the_persisted_role_permissions(PlatformRole role)
    {
        var user = Account(UserStatus.Active, true);
        var service = Create(user, role);

        Assert.Equal(PlatformRolePermissions.For(role).Order(), (await service.GetPermissionsAsync(user.Id, Ct)).Order());
        Assert.Equal(role == PlatformRole.Owner, await service.HasPermissionAsync(user.Id, PlatformPermissions.Settings.Manage, Ct));
        Assert.True(await service.HasPermissionAsync(user.Id, PlatformPermissions.Recoveries.Manage, Ct));
    }

    [Theory]
    [InlineData(UserStatus.Suspended)]
    [InlineData(UserStatus.PendingDeletion)]
    [InlineData(UserStatus.Deleted)]
    public async Task Inactive_operator_has_no_permissions_even_with_an_owner_assignment(UserStatus status)
    {
        var user = Account(status, true);

        Assert.Empty(await Create(user, PlatformRole.Owner).GetPermissionsAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Missing_assignment_or_operator_flag_never_falls_back_to_owner()
    {
        var operatorWithoutRole = Account(UserStatus.Active, true);
        var nonOperator = Account(UserStatus.Active, false);

        Assert.Empty(await Create(operatorWithoutRole, null).GetPermissionsAsync(operatorWithoutRole.Id, Ct));
        Assert.Empty(await Create(nonOperator, PlatformRole.Owner).GetPermissionsAsync(nonOperator.Id, Ct));
        Assert.Empty(await Create(null, PlatformRole.Owner).GetPermissionsAsync(Guid.CreateVersion7(), Ct));
    }

    private static UserAccountRow Account(UserStatus status, bool isOperator) =>
        new(Guid.CreateVersion7(), "Operador", "es-AR", "America/Argentina/Buenos_Aires", status, isOperator, null, null);

    private static PlatformPermissionService Create(UserAccountRow? account, PlatformRole? role) =>
        new(new StubUsers(account), new StubRoles(role), TimeProvider.System, NullLogger<PlatformPermissionService>.Instance);

    private sealed class StubRoles(PlatformRole? role) : IPlatformRoleAssignmentReader
    {
        public Task<PlatformRole?> FindRoleAsync(Guid userId, CancellationToken ct) => Task.FromResult(role);
    }

    private sealed class StubUsers(UserAccountRow? account) : IUserRepository
    {
        public Task<UserAccountRow?> GetByIdAsync(Guid userId, CancellationToken ct) => Task.FromResult(account?.Id == userId ? account : null);
        public Task<Result> RequestDeletionAsync(Guid id, string reason, DateTime utc, int graceDays, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> CancelDeletionAsync(Guid id, DateTime utc, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result> CompleteDeletionAsync(Guid id, DateTime utc, string name, CancellationToken ct) => throw new NotSupportedException();
        public Task<UserAccountRow> CreateAsync(string? name, string culture, string zone, CancellationToken ct) => throw new NotSupportedException();
        public Task SetPrimaryEmailAsync(Guid id, Email email, CancellationToken ct) => throw new NotSupportedException();
        public Task SetPrimaryContactAsync(Guid id, Email? email, PhoneNumber? phone, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateProfileAsync(Guid id, string? name, string culture, string zone, uint version, CancellationToken ct) => throw new NotSupportedException();
        public Task RememberBusinessTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }
}
