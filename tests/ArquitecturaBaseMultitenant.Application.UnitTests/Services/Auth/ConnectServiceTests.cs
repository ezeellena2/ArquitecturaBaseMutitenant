using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

public sealed class ConnectServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reading_business_access_does_not_write_or_mix_personal_membership()
    {
        using var fixture = new ServiceFixture<ConnectService>();
        var account = Account();
        var business = AccessRow(Guid.CreateVersion7(), TenantKind.Business);
        var personal = AccessRow(Guid.CreateVersion7(), TenantKind.Personal);
        var users = new StubUsers(account);
        var unit = new FakeUnitOfWork();
        var service = new ConnectService(users, new StubAccesses([personal, business]),
            new StubPersonalSpaceProvisioner(), new StubPersonalSpaceLock(), new StubTenantScope(), unit,
            fixture.TimeProvider, fixture.Logger);

        var result = await service.GetActiveUserAsync(account.Id, Access.Business, null, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(business.TenantId, result.Value.TenantId);
        Assert.Equal(TenantKind.Business, result.Value.TenantKind);
        Assert.Equal(0, unit.Transactions);
        Assert.Null(users.RememberedTenantId);
    }

    [Fact]
    public async Task Selecting_business_remembers_only_the_chosen_active_organization()
    {
        using var fixture = new ServiceFixture<ConnectService>();
        var business = AccessRow(Guid.CreateVersion7(), TenantKind.Business);
        var account = Account();
        var users = new StubUsers(account);
        var unit = new FakeUnitOfWork();
        var service = new ConnectService(users, new StubAccesses([business]),
            new StubPersonalSpaceProvisioner(), new StubPersonalSpaceLock(), new StubTenantScope(), unit,
            fixture.TimeProvider, fixture.Logger);

        var result = await service.SelectAccessAsync(account.Id, Access.Business, business.TenantId, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(business.TenantId, result.Value.TenantId);
        Assert.Equal(business.TenantId, users.RememberedTenantId);
        Assert.Equal(1, unit.Transactions);
        Assert.Equal(CommitPolicy.OnSuccess, unit.LastPolicy);
    }

    [Fact]
    public async Task Platform_operator_has_no_tenant_claims()
    {
        using var fixture = new ServiceFixture<ConnectService>();
        var account = Account(isPlatformOperator: true);
        var service = new ConnectService(new StubUsers(account), new StubAccesses([]),
            new StubPersonalSpaceProvisioner(), new StubPersonalSpaceLock(), new StubTenantScope(),
            new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

        var result = await service.GetActiveUserAsync(account.Id, Access.Platform, null, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.TenantId);
        Assert.Null(result.Value.TenantKind);
    }

    [Fact]
    public async Task Non_operator_cannot_select_platform()
    {
        using var fixture = new ServiceFixture<ConnectService>();
        var account = Account();
        var service = new ConnectService(new StubUsers(account), new StubAccesses([]),
            new StubPersonalSpaceProvisioner(), new StubPersonalSpaceLock(), new StubTenantScope(),
            new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

        var result = await service.GetActiveUserAsync(account.Id, Access.Platform, null, Ct);

        Assert.Equal(AccessErrors.WrongCode, result.Error.Code);
    }

    [Fact]
    public async Task Consumer_with_foreign_tenant_is_wrong()
    {
        using var fixture = new ServiceFixture<ConnectService>();
        var account = Account();
        var personal = AccessRow(Guid.CreateVersion7(), TenantKind.Personal);
        var service = new ConnectService(new StubUsers(account), new StubAccesses([personal]),
            new StubPersonalSpaceProvisioner(), new StubPersonalSpaceLock(), new StubTenantScope(),
            new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

        var result = await service.GetActiveUserAsync(account.Id, Access.Consumer, Guid.CreateVersion7(), Ct);

        Assert.Equal(AccessErrors.WrongCode, result.Error.Code);
    }

    [Fact]
    public async Task Suspended_identity_cannot_enter_any_access()
    {
        using var fixture = new ServiceFixture<ConnectService>();
        var account = Account(status: UserStatus.Suspended);
        var service = new ConnectService(new StubUsers(account), new StubAccesses([]),
            new StubPersonalSpaceProvisioner(), new StubPersonalSpaceLock(), new StubTenantScope(),
            new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

        var result = await service.GetActiveUserAsync(account.Id, Access.Platform, null, Ct);

        Assert.Equal(AccountErrors.Suspended.Code, result.Error.Code);
    }

    [Fact]
    public async Task Inactive_business_membership_returns_organization_name_for_the_login_state()
    {
        using var fixture = new ServiceFixture<ConnectService>();
        var account = Account();
        var membership = AccessRow(Guid.CreateVersion7(), TenantKind.Business) with
        {
            MemberStatus = MemberStatus.Inactive,
        };
        var service = new ConnectService(new StubUsers(account), new StubAccesses([membership]),
            new StubPersonalSpaceProvisioner(), new StubPersonalSpaceLock(), new StubTenantScope(),
            new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

        var result = await service.GetActiveUserAsync(account.Id, Access.Business, null, Ct);

        Assert.Equal(MemberErrors.InactiveCode, result.Error.Code);
        Assert.Equal("Empresa A", result.Error.Metadata?["organizationName"]);
    }

    [Theory]
    [InlineData(TenantStatus.Suspended, TenantErrors.SuspendedCode)]
    [InlineData(TenantStatus.PendingApproval, TenantErrors.PendingApprovalCode)]
    [InlineData(TenantStatus.Closed, TenantErrors.ClosedCode)]
    public async Task Unavailable_business_reports_its_state_name_and_id_for_the_login_state(
        TenantStatus status, string expectedCode)
    {
        using var fixture = new ServiceFixture<ConnectService>();
        var account = Account();
        var membership = AccessRow(Guid.CreateVersion7(), TenantKind.Business) with { TenantStatus = status };
        var service = new ConnectService(new StubUsers(account), new StubAccesses([membership]),
            new StubPersonalSpaceProvisioner(), new StubPersonalSpaceLock(), new StubTenantScope(),
            new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

        var result = await service.GetActiveUserAsync(account.Id, Access.Business, null, Ct);

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(membership.Name, result.Error.Metadata?["organizationName"]);
        Assert.Equal(membership.TenantId.ToString("D"), result.Error.Metadata?["tenantId"]);
    }

    [Fact]
    public async Task First_consumer_access_creates_personal_space_inside_one_scoped_transaction()
    {
        using var fixture = new ServiceFixture<ConnectService>();
        var events = new List<string>();
        var account = Account();
        var provisioner = new StubPersonalSpaceProvisioner(events);
        var scope = new StubTenantScope(events);
        var unit = new FakeUnitOfWork(events);
        var service = new ConnectService(new StubUsers(account), new StubAccesses([]), provisioner,
            new StubPersonalSpaceLock(events), scope, unit, fixture.TimeProvider, fixture.Logger);

        var result = await service.SelectAccessAsync(account.Id, Access.Consumer, null, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantKind.Personal, result.Value.TenantKind);
        Assert.Equal(provisioner.Draft.Tenant.Id, result.Value.TenantId);
        Assert.Equal(1, unit.Transactions);
        Assert.Equal(["prepare", "enter", "lock", "stage", "commit", "exit"], events);
    }

    private static UserAccountRow Account(UserStatus status = UserStatus.Active, bool isPlatformOperator = false) =>
        new(Guid.CreateVersion7(), "Ana", "es-AR", "America/Argentina/Buenos_Aires", status,
            isPlatformOperator, null, null);

    private static UserTenantAccessRow AccessRow(Guid tenantId, TenantKind kind) =>
        new(tenantId, kind, "Empresa A", null, TenantStatus.Active, MemberStatus.Active,
            new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc));

    private sealed class StubAccesses(IReadOnlyList<UserTenantAccessRow> rows) : IUserTenantAccessReader
    {
        public Task<IReadOnlyList<UserTenantAccessRow>> ListForUserAsync(Guid userId,
            CancellationToken cancellationToken) => Task.FromResult(rows);
    }

    private sealed class StubPersonalSpaceProvisioner(List<string>? events = null) : IPersonalSpaceProvisioner
    {
        public PersonalSpaceDraft Draft { get; } = new(Tenant.CreatePersonal("Personal"),
            TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"),
            "es-AR", "America/Argentina/Buenos_Aires");

        public Task<PersonalSpaceDraft> PrepareAsync(string? cultureCode, string? browserTimeZoneId,
            CancellationToken cancellationToken)
        {
            events?.Add("prepare");
            return Task.FromResult(Draft);
        }

        public void Stage(PersonalSpaceDraft draft, Guid userId) => events?.Add("stage");
    }

    private sealed class StubPersonalSpaceLock(List<string>? events = null) : IPersonalSpaceLock
    {
        public Task LockAsync(Guid userId, CancellationToken cancellationToken)
        {
            events?.Add("lock");
            return Task.CompletedTask;
        }
    }

    private sealed class StubTenantScope(List<string>? events = null) : ITenantScope
    {
        public IDisposable Enter(Guid tenantId)
        {
            events?.Add("enter");
            return new Exit(events);
        }

        private sealed class Exit(List<string>? events) : IDisposable
        {
            public void Dispose() => events?.Add("exit");
        }
    }

    private sealed class StubUsers(UserAccountRow? account) : IUserRepository
    {
        public Guid? RememberedTenantId { get; private set; }

        public Task<UserAccountRow> CreateAsync(string? displayName, string culture, string timeZoneId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<UserAccountRow?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(account?.Id == userId ? account : null);

        public Task SetPrimaryEmailAsync(Guid userId, Email email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetPrimaryContactAsync(Guid userId, Email? email, PhoneNumber? phoneNumber,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpdateProfileAsync(Guid userId, string? displayName, string culture, string timeZoneId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RememberBusinessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
        {
            RememberedTenantId = tenantId;
            return Task.CompletedTask;
        }
    }
}
