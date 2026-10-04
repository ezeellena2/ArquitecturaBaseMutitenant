using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Roles;

public sealed class PlatformPermissionServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string[]> Assignments => new()
    {
        { [.. PlatformPermissions.All] },
        { [PlatformPermissions.Tenants.Read, PlatformPermissions.Recoveries.Manage] },
    };

    [Theory]
    [MemberData(nameof(Assignments))]
    public async Task Active_operator_gets_exactly_the_persisted_permissions(string[] assigned)
    {
        var user = Account(UserStatus.Active, true);
        var service = Create(user, assigned);

        Assert.Equal(assigned, await service.GetPermissionsAsync(user.Id, Ct));
        Assert.Equal(assigned.Contains(PlatformPermissions.Settings.Manage),
            await service.HasPermissionAsync(user.Id, PlatformPermissions.Settings.Manage, Ct));
        Assert.True(await service.HasPermissionAsync(user.Id, PlatformPermissions.Recoveries.Manage, Ct));
    }

    [Theory]
    [InlineData(UserStatus.Suspended)]
    [InlineData(UserStatus.PendingDeletion)]
    [InlineData(UserStatus.Deleted)]
    public async Task Inactive_operator_has_no_permissions_even_with_the_complete_catalog(UserStatus status)
    {
        var user = Account(status, true);

        Assert.Empty(await Create(user, [.. PlatformPermissions.All]).GetPermissionsAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Missing_assignment_or_operator_flag_never_grants_permissions()
    {
        var operatorWithoutAssignment = Account(UserStatus.Active, true);
        var nonOperator = Account(UserStatus.Active, false);

        Assert.Empty(await Create(operatorWithoutAssignment, null).GetPermissionsAsync(operatorWithoutAssignment.Id, Ct));
        Assert.Empty(await Create(nonOperator, [.. PlatformPermissions.All]).GetPermissionsAsync(nonOperator.Id, Ct));
        Assert.Empty(await Create(null, [.. PlatformPermissions.All]).GetPermissionsAsync(Guid.CreateVersion7(), Ct));
    }

    private static UserAccountRow Account(UserStatus status, bool isOperator) =>
        new(Guid.CreateVersion7(), "Operador", "es-AR", "America/Argentina/Buenos_Aires", status, isOperator, null, null);

    private static PlatformPermissionService Create(UserAccountRow? account, string[]? permissions) =>
        new(new StubUsers(account), new StubAssignments(permissions), TimeProvider.System, NullLogger<PlatformPermissionService>.Instance);

    private sealed class StubAssignments(string[]? permissions) : IPlatformPermissionAssignmentReader
    {
        public Task<IReadOnlyList<string>?> FindPermissionsAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>?>(permissions);
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
