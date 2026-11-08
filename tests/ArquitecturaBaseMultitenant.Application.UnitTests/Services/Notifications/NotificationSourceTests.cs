using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Notifications;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Notifications;

/// <summary>Las fuentes del núcleo de la campana (base de la 12), cada una con y sin lo que la habilita.</summary>
public sealed class NotificationSourceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime NowUtc = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(Access.Consumer)]
    [InlineData(Access.Business)]
    public async Task Without_an_own_method_the_account_is_told_to_add_one_from_its_last_verified_method(Access access)
    {
        var managedId = Guid.CreateVersion7();
        var methods = new Methods(
        [
            new(Guid.CreateVersion7(), LoginMethodType.Email, "vieja@empresa.test", false, NowUtc.AddDays(-9), Guid.NewGuid()),
            new(managedId, LoginMethodType.Email, "ana@empresa.test", true, NowUtc.AddDays(-2), Guid.NewGuid()),
            // Propio pero no disponible: sin Google configurado no respalda la cuenta.
            new(Guid.CreateVersion7(), LoginMethodType.Google, "google-sub", false, NowUtc.AddDays(-5), null),
            new(Guid.CreateVersion7(), LoginMethodType.Email, "pendiente@gmail.com", false, null, null),
        ]);
        var source = new PersonalLoginMethodNotificationSource(methods, Availability(googleEnabled: false));

        var item = Assert.Single(await source.ListAsync(Context(access, []), Ct));

        Assert.Equal(UserId, Assert.Single(methods.Calls));
        Assert.Equal("personalLoginMethod:" + managedId.ToString("N"), item.Id);
        Assert.Equal("personalLoginMethod", item.Kind);
        Assert.Empty(item.Params);
        Assert.Equal(NowUtc.AddDays(-2), item.OccurredAtUtc);
        Assert.Equal("/cuenta", item.Link);
        Assert.False(item.Read);
    }

    [Fact]
    public async Task With_an_own_method_available_there_is_no_notice()
    {
        var methods = new Methods(
        [
            new(Guid.CreateVersion7(), LoginMethodType.Email, "ana@empresa.test", true, NowUtc.AddDays(-2), Guid.NewGuid()),
            new(Guid.CreateVersion7(), LoginMethodType.Google, "google-sub", false, NowUtc.AddDays(-1), null),
        ]);
        var source = new PersonalLoginMethodNotificationSource(methods, Availability(googleEnabled: true));

        Assert.Empty(await source.ListAsync(Context(Access.Consumer, []), Ct));
    }

    [Fact]
    public async Task An_operator_account_never_gets_the_personal_method_notice()
    {
        var methods = new Methods([]);
        var source = new PersonalLoginMethodNotificationSource(methods, Availability(googleEnabled: false));

        Assert.Empty(await source.ListAsync(Context(Access.Platform, [.. PlatformPermissions.All]), Ct));
        Assert.Empty(methods.Calls);
    }

    [Fact]
    public async Task Pending_organizations_reach_who_can_see_them_newest_first_with_name_and_card()
    {
        var gamma = Guid.NewGuid();
        var platform = new Platform([Tenant(gamma, "Gamma S.A.", NowUtc.AddHours(-3)), Tenant(Guid.NewGuid(), "Sin alta", null)]);
        var source = new OrganizationPendingNotificationSource(platform);

        var item = Assert.Single(await source.ListAsync(Context(Access.Platform, [PlatformPermissions.Tenants.Read]), Ct));

        var request = Assert.Single(platform.Requests);
        Assert.Equal((TenantStatus.PendingApproval, NotificationListResponse.Limit, "-createdAtUtc"),
            (request.Status, request.PageSize, request.Sort));
        Assert.Equal(("organizationPending:" + gamma.ToString("N"), "organizationPending"), (item.Id, item.Kind));
        Assert.Equal("Gamma S.A.", item.Params["name"]);
        Assert.Equal(NowUtc.AddHours(-3), item.OccurredAtUtc);
        Assert.Equal("/plataforma/organizaciones/" + gamma.ToString("D"), item.Link);
    }

    [Theory]
    [InlineData(Access.Platform, PlatformPermissions.Recoveries.Manage)]
    [InlineData(Access.Business, PlatformPermissions.Tenants.Read)]
    [InlineData(Access.Consumer, PlatformPermissions.Tenants.Read)]
    public async Task Pending_organizations_are_not_read_without_the_platform_permission(Access access, string permission)
    {
        var platform = new Platform([Tenant(Guid.NewGuid(), "Gamma S.A.", NowUtc)]);
        var source = new OrganizationPendingNotificationSource(platform);

        Assert.Empty(await source.ListAsync(Context(access, [permission]), Ct));
        Assert.Empty(platform.Requests);
    }

    [Fact]
    public async Task Recovery_requests_reach_who_reviews_them_with_the_name_and_the_masked_method()
    {
        var named = Recovery("Pablo Ortiz", LoginMethodType.Email, "pablo.ortiz@gmail.com", NowUtc.AddMinutes(-40));
        var unknown = Recovery(null, LoginMethodType.Phone, "+5491122334455", NowUtc.AddMinutes(-90));
        var recoveries = new Recoveries([named, unknown]);
        var source = new RecoveryRequestedNotificationSource(recoveries);

        var items = await source.ListAsync(Context(Access.Platform, [PlatformPermissions.Recoveries.Manage]), Ct);

        var request = Assert.Single(recoveries.Requests);
        Assert.Equal((AccountRecoveryStatus.Pending, NotificationListResponse.Limit, "-createdAt"),
            (request.Status, request.PageSize, request.Sort));
        Assert.Equal(["recoveryRequested:" + named.Id.ToString("N"), "recoveryRequested:" + unknown.Id.ToString("N")],
            items.Select(item => item.Id));
        Assert.All(items, item => Assert.Equal("/plataforma/recuperaciones?status=Pending", item.Link));
        Assert.Equal("Pablo Ortiz", items[0].Params["name"]);
        Assert.Equal(("Email", "pa•••@gmail.com"), (items[0].Params["methodType"], items[0].Params["method"]));
        Assert.False(items[1].Params.ContainsKey("name"));
        Assert.Equal(("Phone", "•••• 4455"), (items[1].Params["methodType"], items[1].Params["method"]));
        Assert.Equal(NowUtc.AddMinutes(-40), items[0].OccurredAtUtc);
        Assert.DoesNotContain(items.SelectMany(item => item.Params.Values), value => value.Contains("pablo.ortiz", StringComparison.Ordinal)
            || value.Contains("22334455", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(Access.Platform, PlatformPermissions.Tenants.Read)]
    [InlineData(Access.Business, PlatformPermissions.Recoveries.Manage)]
    public async Task Recovery_requests_are_not_read_without_the_review_permission(Access access, string permission)
    {
        var recoveries = new Recoveries([Recovery("Pablo Ortiz", LoginMethodType.Email, "pablo@gmail.com", NowUtc)]);
        var source = new RecoveryRequestedNotificationSource(recoveries);

        Assert.Empty(await source.ListAsync(Context(access, [permission]), Ct));
        Assert.Empty(recoveries.Requests);
    }

    private static NotificationContext Context(Access access, IReadOnlyCollection<string> permissions) =>
        new(UserId, access, permissions);

    private static LoginMethodAvailabilityPolicy Availability(bool googleEnabled) =>
        new([], new Google(googleEnabled), new Memberships());

    private static PlatformTenantResponse Tenant(Guid id, string name, DateTime? createdAtUtc) =>
        new(id, name, null, TenantStatus.PendingApproval, TenantRegistrationSource.SelfService, createdAtUtc, 1, 1, 1);

    private static PlatformRecoveryRow Recovery(string? name, LoginMethodType type, string value, DateTime createdAtUtc) =>
        new(Guid.CreateVersion7(), createdAtUtc, name is null ? null : Guid.NewGuid(), name, type, value,
            LoginMethodType.Email, "nueva@gmail.com", AccountRecoveryStatus.Pending, null, null, null, 1);

    private sealed class Methods(IReadOnlyList<LoginMethodRow> rows) : ILoginMethodReader
    {
        public List<Guid> Calls { get; } = [];

        public Task<IReadOnlyList<LoginMethodRow>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            Calls.Add(userId);
            return Task.FromResult(rows);
        }
    }

    private sealed class Google(bool enabled) : IGoogleAvailability
    {
        public bool IsEnabled => enabled;
    }

    private sealed class Memberships : ILoginMethodMembershipReader
    {
        public Task<bool> IsActiveAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class Platform(IReadOnlyList<PlatformTenantResponse> pending) : IPlatformReader
    {
        public List<ListPlatformTenantsRequest> Requests { get; } = [];

        public Task<PagedResult<PlatformTenantResponse>> ListTenantsAsync(ListPlatformTenantsRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new PagedResult<PlatformTenantResponse>(pending, request.Page, request.PageSize, pending.Count));
        }

        public Task<PlatformTenantFilterCounts> CountTenantsAsync(ListPlatformTenantsRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<PlatformTenantResponse?> FindTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlatformTenantDetailRow?> FindTenantDetailAsync(Guid tenantId, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListActiveTenantOwnerIdsAsync(Guid tenantId, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class Recoveries(IReadOnlyList<PlatformRecoveryRow> pending) : IAccountRecoveryReader
    {
        public List<ListPlatformRecoveriesRequest> Requests { get; } = [];

        public Task<PagedResult<PlatformRecoveryRow>> ListAsync(ListPlatformRecoveriesRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new PagedResult<PlatformRecoveryRow>(pending, request.Page, request.PageSize, pending.Count));
        }

        public Task<PlatformRecoveryFilterCounts> CountAsync(ListPlatformRecoveriesRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
