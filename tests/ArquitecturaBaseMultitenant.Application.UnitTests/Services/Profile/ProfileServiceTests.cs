using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Profile;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Validation.Profile;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Profile;

/// <summary>
/// Comprueba preferencias, permisos por acceso y advertencias de métodos de ingreso del perfil. Exige cultura y
/// zona habilitadas y un método propio utilizable; los permisos de Plataforma siempre reflejan la asignación vigente.
/// </summary>
public sealed class ProfileServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Consumer_currency_keeps_active_space_preferences_without_exposing_organization_country()
    {
        using var fixture = new ServiceFixture<ProfileService>();
        var userId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var memberships = new StubAccesses([
            new(tenantId, TenantKind.Personal, "Personal", null, TenantStatus.Active, MemberStatus.Active, null),
        ]);
        var settings = new StubSettings(new(tenantId, "es-AR", "America/Argentina/Buenos_Aires", "ARS"));
        var service = CreateService(new StubCurrentUser(userId, Access.Consumer),
            new FakeTenantContext { TenantId = tenantId, TenantKind = TenantKind.Personal },
            new StubUsers(Account(userId) with { Culture = "en-US" }), memberships, settings, fixture);

        var result = await service.GetAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("ARS", result.Value.CurrencyCode);
        Assert.Null(result.Value.OrganizationCountryCode);
        Assert.Equal(1, settings.Reads);
    }

    [Theory]
    [InlineData(Access.Business, TenantKind.Business, TenantStatus.Active, MemberStatus.Active, "es-AR", "en-US", "AR", 1)]
    [InlineData(Access.Business, TenantKind.Business, TenantStatus.Active, MemberStatus.Active, "en-US", "es-AR", "US", 1)]
    [InlineData(Access.Consumer, TenantKind.Personal, TenantStatus.Active, MemberStatus.Active, "es-AR", "en-US", null, 1)]
    [InlineData(Access.Platform, TenantKind.Business, TenantStatus.Active, MemberStatus.Active, "es-AR", "en-US", null, 1)]
    [InlineData(Access.Business, TenantKind.Personal, TenantStatus.Active, MemberStatus.Active, "es-AR", "en-US", null, 1)]
    [InlineData(Access.Business, TenantKind.Business, TenantStatus.Suspended, MemberStatus.Active, "es-AR", "en-US", null, 0)]
    [InlineData(Access.Business, TenantKind.Business, TenantStatus.Closed, MemberStatus.Active, "es-AR", "en-US", null, 0)]
    [InlineData(Access.Business, TenantKind.Business, TenantStatus.Active, MemberStatus.Inactive, "es-AR", "en-US", null, 0)]
    [InlineData(Access.Business, TenantKind.Business, TenantStatus.Active, MemberStatus.Active, null, "en-US", null, 1)]
    [InlineData(Access.Business, TenantKind.Business, TenantStatus.Active, MemberStatus.Active, "zz-ZZ", "es-AR", null, 1)]
    public async Task Me_country_comes_from_the_available_business_organization_and_reuses_the_settings_read(
        Access access, TenantKind kind, TenantStatus tenantStatus, MemberStatus memberStatus,
        string? organizationCulture, string personalCulture, string? expectedCountry, int expectedReads)
    {
        using var fixture = new ServiceFixture<ProfileService>();
        var userId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var memberships = new StubAccesses([
            new(tenantId, kind, "Perfil", "perfil", tenantStatus, memberStatus, null),
        ]);
        var settings = new StubSettings(organizationCulture is null ? null
            : new(tenantId, organizationCulture, "America/Argentina/Buenos_Aires", "ARS"));
        // Sin permisos de Configuración: el perfil debe seguir resolviendo el país para Empresas.
        var service = CreateService(new StubCurrentUser(userId, access),
            new FakeTenantContext { TenantId = tenantId, TenantKind = kind },
            new StubUsers(Account(userId) with { Culture = personalCulture }), memberships, settings, fixture);

        var result = await service.GetAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(personalCulture, result.Value.Culture);
        Assert.Equal(expectedCountry, result.Value.OrganizationCountryCode);
        Assert.Equal(expectedReads, settings.Reads);
        Assert.Empty(result.Value.Permissions);
    }

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
            new ProfilePreferencesResolver(new StubSettings(null), catalog, catalog), new StubMethods([method]),
            new LoginMethodAvailabilityPolicy([], new StubGoogle(available), new StubMemberships()),
            new LegalAcceptanceGuard(new StubLegal(), fixture.TimeProvider),
            new ProfileEffectivePermissionsBuilder(new StubPermissions(), new StubPlatformPermissions()), new StubFeatures());

        var profile = await snapshots.BuildAsync(id, Access.Consumer, null, Ct);

        Assert.Equal(warning, profile!.NeedsPersonalLoginMethod);
    }

    [Theory]
    [InlineData(Access.Business, "managed", "ana@empresa-a.test")]
    [InlineData(Access.Business, "unmanaged", "ana@example.test")]
    [InlineData(Access.Consumer, null, "ana@example.test")]
    public async Task Me_email_is_the_one_of_the_active_profile(Access access, string? organization, string expected)
    {
        var id = Guid.CreateVersion7();
        var managedId = Guid.CreateVersion7();
        var unmanagedId = Guid.CreateVersion7();
        var catalog = new JsonReferenceDataCatalog();
        using var fixture = new ServiceFixture<ProfileService>();
        var verifiedAtUtc = fixture.TimeProvider.GetUtcNow().UtcDateTime;
        var snapshots = new ProfileSnapshotBuilder(new StubUsers(Account(id)), new StubAccesses([]),
            new ProfilePreferencesResolver(new StubSettings(null), catalog, catalog), new StubMethods([
                new(Guid.CreateVersion7(), LoginMethodType.Email, "ana@example.test", true, verifiedAtUtc, null),
                new(Guid.CreateVersion7(), LoginMethodType.Email, "ana@empresa-a.test", false, verifiedAtUtc, managedId),
            ]),
            new LoginMethodAvailabilityPolicy([], new StubGoogle(), new StubMemberships()),
            new LegalAcceptanceGuard(new StubLegal(), fixture.TimeProvider),
            new ProfileEffectivePermissionsBuilder(new StubPermissions(), new StubPlatformPermissions()), new StubFeatures());
        Guid? activeTenantId = organization switch { "managed" => managedId, "unmanaged" => unmanagedId, _ => null };

        var profile = await snapshots.BuildAsync(id, access, activeTenantId, Ct);

        Assert.Equal(expected, profile!.Email?.Value);
    }

    [Fact]
    public async Task Update_requires_enabled_culture_and_time_zone_from_catalogs()
    {
        var catalog = new JsonReferenceDataCatalog();
        var validator = new UpdateMeRequestValidator(catalog, catalog);

        Assert.True((await validator.ValidateAsync(new UpdateMeRequest(null, null, "es-AR",
            "America/Argentina/Buenos_Aires", 5), Ct)).IsValid);

        var invalid = await validator.ValidateAsync(new UpdateMeRequest(null, null, "zz-ZZ", "Unknown/Zone", 5), Ct);
        Assert.Equal(["Culture", "TimeZoneId"], invalid.Errors.Select(error => error.PropertyName));
    }

    [Fact]
    public async Task Optional_name_parts_have_their_individual_limits()
    {
        var catalog = new JsonReferenceDataCatalog();
        var validator = new UpdateMeRequestValidator(catalog, catalog);

        var invalid = await validator.ValidateAsync(new UpdateMeRequest(
            new string('A', TextLimits.FirstName + 1), new string('B', TextLimits.LastName + 1), "es-AR", "America/Argentina/Buenos_Aires", 5), Ct);

        Assert.Equal(["FirstName", "LastName"], invalid.Errors.Select(error => error.PropertyName));
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
                MemberStatus.Active, null, "TenantAdmin"),
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
        Assert.Equal("TenantAdmin", result.Value.Organizations[0].RoleName);
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

    [Theory]
    [InlineData(TenantStatus.Active, MemberStatus.Active, true)]
    [InlineData(TenantStatus.Suspended, MemberStatus.Active, false)]
    [InlineData(TenantStatus.Closed, MemberStatus.Active, false)]
    [InlineData(TenantStatus.Active, MemberStatus.Inactive, false)]
    public async Task Me_lists_features_only_for_an_available_active_business_space(TenantStatus tenantStatus,
        MemberStatus memberStatus, bool listed)
    {
        using var fixture = new ServiceFixture<ProfileService>();
        var userId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var accesses = new StubAccesses([
            new(tenantId, TenantKind.Business, "Empresa A", "empresa-a", tenantStatus, memberStatus, null),
        ]);
        var features = new StubFeatures(["test-business"]);
        var service = CreateService(new StubCurrentUser(userId, Access.Business),
            new FakeTenantContext { TenantId = tenantId, TenantKind = TenantKind.Business },
            new StubUsers(Account(userId)), accesses, new StubSettings(null), fixture, features: features);

        var result = await service.GetAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(listed ? ["test-business"] : [], result.Value.Features);
        Assert.Equal(listed ? 1 : 0, features.Calls);
    }

    [Fact]
    public async Task Platform_me_always_has_an_empty_feature_list()
    {
        using var fixture = new ServiceFixture<ProfileService>();
        var userId = Guid.CreateVersion7();
        var features = new StubFeatures(["test-business"]);
        var service = CreateService(new StubCurrentUser(userId, Access.Platform), new FakeTenantContext(),
            new StubUsers(Account(userId) with { IsPlatformOperator = true }), new StubAccesses([]),
            new StubSettings(null), fixture, features: features);

        var result = await service.GetAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Features);
        Assert.Empty(result.Value.Features);
        Assert.Equal(0, features.Calls);
    }

    [Fact]
    public async Task Platform_me_has_exactly_the_persisted_permissions_and_reads_changes_without_a_cache()
    {
        using var fixture = new ServiceFixture<ProfileService>();
        var userId = Guid.CreateVersion7();
        string[] assigned = [PlatformPermissions.Tenants.Read, PlatformPermissions.Audit.Read];
        var assignments = new StubPlatformAssignments(assigned);
        var service = CreateService(new StubCurrentUser(userId, Access.Platform), new FakeTenantContext(),
            new StubUsers(Account(userId) with { IsPlatformOperator = true }), new StubAccesses([]),
            new StubSettings(null), fixture, platformAssignments: assignments);

        var first = await service.GetAsync(Ct);

        Assert.True(first.IsSuccess);
        Assert.Equal(assigned, first.Value.EffectivePermissions.Platform);
        Assert.Empty(first.Value.EffectivePermissions.Organization);
        Assert.Empty(first.Value.EffectivePermissions.Companies);
        Assert.Empty(first.Value.Permissions);

        assignments.Permissions = [PlatformPermissions.Audit.Read];
        var changed = await service.GetAsync(Ct);
        Assert.Equal([PlatformPermissions.Audit.Read], changed.Value.EffectivePermissions.Platform);
    }

    [Theory]
    [InlineData(UserStatus.Suspended, true, true, false)]
    [InlineData(UserStatus.PendingDeletion, true, true, false)]
    [InlineData(UserStatus.Active, true, false, false)]
    [InlineData(UserStatus.Active, false, true, false)]
    [InlineData(UserStatus.Active, true, true, true)]
    public async Task Platform_me_has_no_permissions_without_an_active_accepted_operator_and_assignment(
        UserStatus status, bool isOperator, bool hasAssignment, bool hasPendingInvitation)
    {
        using var fixture = new ServiceFixture<ProfileService>();
        var userId = Guid.CreateVersion7();
        var account = Account(userId) with
        {
            Status = status,
            IsPlatformOperator = isOperator,
            OperatorInvitationExpiresAtUtc = hasPendingInvitation
                ? fixture.TimeProvider.GetUtcNow().UtcDateTime.AddDays(7) : null,
        };
        var service = CreateService(new StubCurrentUser(userId, Access.Platform), new FakeTenantContext(),
            new StubUsers(account), new StubAccesses([]), new StubSettings(null), fixture,
            platformAssignments: new StubPlatformAssignments(hasAssignment ? [.. PlatformPermissions.All] : null));

        var result = await service.GetAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.EffectivePermissions.Platform);
        Assert.Empty(result.Value.Permissions);
    }

    [Theory]
    [InlineData(Access.Consumer)]
    [InlineData(Access.Business)]
    public async Task Person_accesses_never_read_platform_permissions_and_keep_the_organization_and_company_permissions(
        Access access)
    {
        using var fixture = new ServiceFixture<ProfileService>();
        var userId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var companyId = Guid.CreateVersion7();
        var accesses = new StubAccesses([
            new(tenantId, TenantKind.Business, "Empresa A", "empresa-a", TenantStatus.Active,
                MemberStatus.Active, null),
        ]);
        var platform = new StubPlatformPermissions([.. PlatformPermissions.All]);
        var service = CreateService(new StubCurrentUser(userId, access),
            new FakeTenantContext { TenantId = access == Access.Business ? tenantId : null },
            new StubUsers(Account(userId)), accesses, new StubSettings(null), fixture,
            permissions: new StubPermissions([Permissions.Users.Read],
                new Dictionary<Guid, IReadOnlyList<string>> { [companyId] = [Permissions.Company.Members.Read] }),
            platformPermissions: platform);

        var result = await service.GetAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.EffectivePermissions.Platform);
        Assert.Equal(0, platform.Calls);
        Assert.Equal(access == Access.Business ? [Permissions.Users.Read] : [], result.Value.EffectivePermissions.Organization);
        Assert.Equal(result.Value.EffectivePermissions.Organization, result.Value.Permissions);
        if (access == Access.Business)
            Assert.Equal([Permissions.Company.Members.Read], result.Value.EffectivePermissions.Companies[companyId]);
        else Assert.Empty(result.Value.EffectivePermissions.Companies);
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

        var invalid = await service.UpdateAsync(new UpdateMeRequest("Ana", null, "zz-ZZ", "Unknown/Zone", 5), Ct);
        Assert.True(invalid.IsFailure);
        Assert.Equal(0, unitOfWork.Transactions);

        var updated = await service.UpdateAsync(new UpdateMeRequest("Ana María", "Pérez", "en-US",
            "America/Argentina/Buenos_Aires", 5), Ct);
        Assert.True(updated.IsSuccess);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(CommitPolicy.OnSuccess, unitOfWork.LastPolicy);
        Assert.Equal((userId, "Ana María", "Pérez", "en-US", "America/Argentina/Buenos_Aires"), users.LastUpdate);
        // La respuesta lleva la versión del perfil que dejó el guardado, no la que mandó la pantalla.
        Assert.Equal(6u, updated.Value.Version);
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
        ServiceFixture<ProfileService> fixture, IUnitOfWork? unitOfWork = null, IFeatureService? features = null,
        IPlatformPermissionAssignmentReader? platformAssignments = null, IPermissionService? permissions = null,
        IPlatformPermissionService? platformPermissions = null)
    {
        var catalog = new JsonReferenceDataCatalog();
        var availability = new LoginMethodAvailabilityPolicy([], new StubGoogle(), new StubMemberships());
        var effectivePermissions = new ProfileEffectivePermissionsBuilder(permissions ?? new StubPermissions(),
            platformPermissions ?? new PlatformPermissionService(users, platformAssignments ?? new StubPlatformAssignments(null),
                fixture.TimeProvider, NullLogger<PlatformPermissionService>.Instance));
        var snapshots = new ProfileSnapshotBuilder(users, accesses, new ProfilePreferencesResolver(settings, catalog, catalog), new StubMethods(),
            availability, new LegalAcceptanceGuard(new StubLegal(), fixture.TimeProvider), effectivePermissions,
            features ?? new StubFeatures());
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
        public (Guid UserId, string? FirstName, string? LastName, string Culture, string TimeZoneId)? LastUpdate { get; private set; }

        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result> RequestDeletionAsync(Guid userId, string reason,
            DateTime requestedAtUtc, int graceDays, CancellationToken ct) => throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result> RequestDeletionByOperatorAsync(Guid userId, Guid operatorId,
            string reason, DateTime requestedAtUtc, int graceDays, CancellationToken ct) => throw new NotSupportedException();
        public Task SetPlatformOperatorAsync(Guid userId, bool isOperator, CancellationToken ct) => throw new NotSupportedException();
        public Task InvitePlatformOperatorAsync(Guid userId, DateTime expiresAtUtc, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result> RenewOperatorInvitationAsync(Guid userId, DateTime expiresAtUtc,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result> AcceptOperatorInvitationAsync(Guid userId, DateTime acceptedAtUtc,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result> DiscardOperatorInvitationAsync(Guid userId, DateTime discardedAtUtc,
            string deletedDisplayName, CancellationToken ct) => throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result> CancelDeletionAsync(Guid userId,
            DateTime cancelledAtUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result> CompleteDeletionAsync(Guid userId,
            DateTime deletedAtUtc, string deletedDisplayName, CancellationToken ct) => throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result> SuspendAsync(Guid userId, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result<UserStatus>> ReactivateAsync(Guid userId,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<UserAccountRow> CreateAsync(string? displayName, string culture, string timeZoneId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<UserAccountRow?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(account?.Id == userId ? account : null);

        public Task SetPrimaryEmailAsync(Guid userId, Email email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetPrimaryContactAsync(Guid userId, Email? email, PhoneNumber? phoneNumber,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task FillMissingProfileNamesAsync(Guid userId, string? firstName, string? lastName, CancellationToken ct) => throw new NotSupportedException();

        public Task<ArquitecturaBaseMultitenant.Domain.Results.Result<uint>> UpdateProfileAsync(Guid userId, string? firstName, string? lastName, string culture, string timeZoneId,
            uint expectedVersion, CancellationToken cancellationToken)
        {
            LastUpdate = (userId, firstName, lastName, culture, timeZoneId);
            return Task.FromResult(ArquitecturaBaseMultitenant.Domain.Results.Result.Success(expectedVersion + 1));
        }

        public Task RememberBusinessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubMethods(IReadOnlyList<LoginMethodRow>? rows = null) : ILoginMethodReader
    {
        public Task<IReadOnlyList<LoginMethodRow>> ListByUserIdAsync(Guid userId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LoginMethodRow>>(rows ?? []);
    }

    private sealed class StubPermissions(IReadOnlyList<string>? organization = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? companies = null) : IPermissionService
    {
        public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(organization ?? []);
        public Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(companies ?? new Dictionary<Guid, IReadOnlyList<string>>());
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubPlatformAssignments(string[]? permissions) : IPlatformPermissionAssignmentReader
    {
        public string[]? Permissions { get; set; } = permissions;
        public Task<IReadOnlyList<string>?> FindPermissionsAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>?>(Permissions);
    }

    private sealed class StubPlatformPermissions(IReadOnlyList<string>? assigned = null) : IPlatformPermissionService
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<string>>(assigned ?? []);
        }
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class StubGoogle(bool enabled = false) : IGoogleAvailability { public bool IsEnabled => enabled; }
    private sealed class StubFeatures(IReadOnlyList<string>? enabled = null) : IFeatureService
    {
        public int Calls { get; private set; }

        public Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<IReadOnlyList<string>> ListEnabledAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(enabled ?? []);
        }
    }
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
        public Task<IReadOnlyList<ArquitecturaBaseMultitenant.Application.Models.Platform.LegalVersionRow>> ListVersionsAsync(
            CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ArquitecturaBaseMultitenant.Application.Models.Platform.PlatformLegalContentResponse>> ListContentsAsync(
            Guid documentId, CancellationToken ct) => throw new NotSupportedException();
    }
}
