using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Profile;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Validation.Profile;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Profile;

public sealed class ProfileServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    public async Task Personal_method_warning_requires_verified_available_own_method(
        bool verified, bool available, bool managed, bool warning)
    {
        var id = Guid.CreateVersion7();
        var catalog = new JsonReferenceDataCatalog();
        using var fixture = new ServiceFixture<ProfileService>();
        var method = new LoginMethodRow(Guid.CreateVersion7(), LoginMethodType.Google, "subject", true,
            verified ? fixture.TimeProvider.GetUtcNow().UtcDateTime : null, managed ? Guid.CreateVersion7() : null);
        var snapshots = new ProfileSnapshotBuilder(new StubUsers(Account(id)), new StubAccesses([]),
            new StubSettings(null), catalog, catalog, new StubMethods([method]),
            new LoginMethodAvailability([], new StubGoogle(available), new StubMemberships()),
            new LegalAcceptanceGuard(new StubLegal(), fixture.TimeProvider));

        var profile = await snapshots.BuildAsync(id, Access.Consumer, null, Ct);

        Assert.Equal(warning, profile!.NeedsPersonalLoginMethod);
    }

    [Fact]
    public async Task Update_requires_enabled_culture_and_time_zone_from_catalogs()
    {
        var catalog = new JsonReferenceDataCatalog();
        var validator = new UpdateMeRequestValidator(catalog, catalog);

        Assert.True((await validator.ValidateAsync(new UpdateMeRequest(null, "es-AR",
            "America/Argentina/Buenos_Aires", 5), Ct)).IsValid);

        var invalid = await validator.ValidateAsync(new UpdateMeRequest(null, "zz-ZZ", "Unknown/Zone", 5), Ct);
        Assert.Equal(["Culture", "TimeZoneId"], invalid.Errors.Select(error => error.PropertyName));
    }

    [Fact]
    public async Task Optional_display_name_has_person_name_limit()
    {
        var catalog = new JsonReferenceDataCatalog();
        var validator = new UpdateMeRequestValidator(catalog, catalog);

        var invalid = await validator.ValidateAsync(new UpdateMeRequest(
            new string('A', TextLimits.PersonName + 1), "es-AR", "America/Argentina/Buenos_Aires", 5), Ct);

        Assert.Equal("DisplayName", Assert.Single(invalid.Errors).PropertyName);
    }

    [Theory]
    [InlineData(TenantStatus.Active, MemberStatus.Active, true)]
    [InlineData(TenantStatus.Active, MemberStatus.Inactive, false)]
    [InlineData(TenantStatus.Suspended, MemberStatus.Active, false)]
    [InlineData(TenantStatus.Closed, MemberStatus.Active, false)]
    public void Profile_selection_requires_active_tenant_and_membership(
        TenantStatus tenantStatus, MemberStatus memberStatus, bool expected)
    {
        var profile = new OrganizationSummary(Guid.CreateVersion7(), "Empresa A", null,
            tenantStatus, null, memberStatus);

        Assert.Equal(expected, profile.IsSelectable);
    }

    [Fact]
    public async Task Get_includes_all_business_profiles_and_marks_suspended_one_unselectable()
    {
        using var fixture = new ServiceFixture<ProfileService>();
        var userId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var suspendedId = Guid.CreateVersion7();
        var personalId = Guid.CreateVersion7();
        var accesses = new StubAccesses([
            new(tenantId, TenantKind.Business, "Empresa A", "empresa-a", TenantStatus.Active,
                MemberStatus.Active, null),
            new(suspendedId, TenantKind.Business, "Empresa B", "empresa-b", TenantStatus.Suspended,
                MemberStatus.Active, null),
            new(personalId, TenantKind.Personal, "Personal", null, TenantStatus.Active,
                MemberStatus.Active, null),
        ]);
        var current = new StubCurrentUser(userId, Access.Business);
        var context = new FakeTenantContext { TenantId = tenantId, TenantKind = TenantKind.Business };
        var settings = new StubSettings(new(tenantId, "es-AR", "America/Argentina/Buenos_Aires", "ARS"));
        var service = CreateService(current, context, new StubUsers(Account(userId) with { Culture = "en-US" }), accesses,
            settings, fixture);

        var result = await service.GetAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, accesses.QueriedUserId);
        Assert.Equal(Access.Business, result.Value.Access);
        Assert.Equal(tenantId, result.Value.ActiveTenantId);
        Assert.True(result.Value.HasPersonalSpace);
        Assert.Equal(2, result.Value.Organizations.Count);
        Assert.True(result.Value.Organizations[0].IsSelectable);
        Assert.False(result.Value.Organizations[1].IsSelectable);
        Assert.Equal(MemberStatus.Active, result.Value.Organizations[1].MemberStatus);
        Assert.Equal("en-US", result.Value.Culture);
        Assert.Equal("ARS", result.Value.CurrencyCode);
        Assert.Equal(1, settings.Reads);
        Assert.Empty(result.Value.Permissions);
    }

    [Fact]
    public async Task Suspended_business_me_keeps_profile_without_private_settings_or_permissions()
    {
        using var fixture = new ServiceFixture<ProfileService>();
        var userId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var accesses = new StubAccesses([
            new(tenantId, TenantKind.Business, "Empresa B", "empresa-b", TenantStatus.Suspended,
                MemberStatus.Active, null),
        ]);
        var settings = new StubSettings(new(tenantId, "es-AR", "America/Argentina/Buenos_Aires", "ARS"));
        var service = CreateService(new StubCurrentUser(userId, Access.Business),
            new FakeTenantContext { TenantId = tenantId, TenantKind = TenantKind.Business },
            new StubUsers(Account(userId) with { Culture = "en-US" }), accesses, settings, fixture);

        var result = await service.GetAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("en-US", result.Value.Culture);
        Assert.Equal("USD", result.Value.CurrencyCode);
        Assert.False(Assert.Single(result.Value.Organizations).IsSelectable);
        Assert.Empty(result.Value.Permissions);
        Assert.Equal(0, settings.Reads);
    }

    [Fact]
    public async Task Update_validates_before_transaction_then_writes_once()
    {
        var catalog = new JsonReferenceDataCatalog();
        using var fixture = new ServiceFixture<ProfileService>(new UpdateMeRequestValidator(catalog, catalog));
        var userId = Guid.CreateVersion7();
        var users = new StubUsers(Account(userId));
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(new StubCurrentUser(userId, Access.Consumer), new FakeTenantContext(),
            users, new StubAccesses([]), new StubSettings(null), fixture, unitOfWork);

        var invalid = await service.UpdateAsync(new UpdateMeRequest("Ana", "zz-ZZ", "Unknown/Zone", 5), Ct);
        Assert.True(invalid.IsFailure);
        Assert.Equal(0, unitOfWork.Transactions);

        var updated = await service.UpdateAsync(new UpdateMeRequest("Ana", "en-US",
            "America/Argentina/Buenos_Aires", 5), Ct);
        Assert.True(updated.IsSuccess);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(CommitPolicy.OnSuccess, unitOfWork.LastPolicy);
        Assert.Equal((userId, "Ana", "en-US", "America/Argentina/Buenos_Aires"), users.LastUpdate);
    }

    private static UserAccountRow Account(Guid id) => new(id, "Ana", "es-AR",
        "America/Argentina/Buenos_Aires", UserStatus.Active, false,
        Email.Create("ana@example.test").Value, null);

    private sealed class StubCurrentUser(Guid userId, Access access) : ICurrentUser
    {
        public Guid? UserId => userId;
        public Access? Access => access;
    }

    private static ProfileService CreateService(ICurrentUser current, ITenantContext context,
        IUserRepository users, IUserTenantAccessReader accesses, ITenantSettingsReader settings,
        ServiceFixture<ProfileService> fixture, IUnitOfWork? unitOfWork = null)
    {
        var catalog = new JsonReferenceDataCatalog();
        var availability = new LoginMethodAvailability([], new StubGoogle(), new StubMemberships());
        var snapshots = new ProfileSnapshotBuilder(users, accesses, settings, catalog, catalog, new StubMethods(),
            availability, new LegalAcceptanceGuard(new StubLegal(), fixture.TimeProvider));
        return new ProfileService(current, context, snapshots, users, fixture.Validator,
            unitOfWork ?? new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);
    }

    private sealed class StubAccesses(IReadOnlyList<UserTenantAccessRow> rows) : IUserTenantAccessReader
    {
        public Guid? QueriedUserId { get; private set; }

        public Task<IReadOnlyList<UserTenantAccessRow>> ListForUserAsync(Guid userId,
            CancellationToken cancellationToken)
        {
            QueriedUserId = userId;
            return Task.FromResult(rows);
        }
    }

    private sealed class StubSettings(TenantSettingsRow? row) : ITenantSettingsReader
    {
        public int Reads { get; private set; }

        public Task<TenantSettingsRow?> FindCurrentAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(row);
        }
    }

    private sealed class StubUsers(UserAccountRow? account) : IUserRepository
    {
        public (Guid UserId, string? DisplayName, string Culture, string TimeZoneId)? LastUpdate { get; private set; }

        public Task<UserAccountRow> CreateAsync(string? displayName, string culture, string timeZoneId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<UserAccountRow?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(account?.Id == userId ? account : null);

        public Task SetPrimaryEmailAsync(Guid userId, Email email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetPrimaryContactAsync(Guid userId, Email? email, PhoneNumber? phoneNumber,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpdateProfileAsync(Guid userId, string? displayName, string culture, string timeZoneId,
            uint expectedVersion, CancellationToken cancellationToken)
        {
            LastUpdate = (userId, displayName, culture, timeZoneId);
            return Task.CompletedTask;
        }

        public Task RememberBusinessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubMethods(IReadOnlyList<LoginMethodRow>? rows = null) : ILoginMethodReader
    {
        public Task<IReadOnlyList<LoginMethodRow>> ListByUserIdAsync(Guid userId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LoginMethodRow>>(rows ?? []);
    }

    private sealed class StubGoogle(bool enabled = false) : IGoogleAvailability { public bool IsEnabled => enabled; }
    private sealed class StubMemberships : ILoginMethodMembershipReader
    {
        public Task<bool> IsActiveAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
    }
    private sealed class StubLegal : ILegalReader
    {
        public Task<IReadOnlyList<PendingLegalDocumentResponse>> ListPendingAsync(Guid userId, DateTime nowUtc,
            CancellationToken ct) => Task.FromResult<IReadOnlyList<PendingLegalDocumentResponse>>([]);
        public Task<LegalDocumentRow?> FindCurrentAsync(LegalDocumentKind kind, string culture, DateTime nowUtc,
            CancellationToken ct) => throw new NotSupportedException();
    }
}
