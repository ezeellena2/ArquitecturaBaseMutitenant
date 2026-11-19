using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Notifications;

public sealed class NotificationServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Theory]
    [InlineData(Access.Consumer)]
    [InlineData(Access.Business)]
    [InlineData(Access.Platform)]
    public async Task Each_access_reads_its_sources_with_the_permissions_of_that_access(Access access)
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var source = new ProbeSource();
        var service = Service(fixture, [source], access);

        var result = await service.ListAsync(Ct);

        Assert.True(result.IsSuccess);
        var context = Assert.Single(source.Contexts);
        Assert.Equal(access, context.Access);
        switch (access)
        {
            case Access.Consumer:
                Assert.Empty(context.Permissions);
                Assert.Empty(context.CompaniesWhere(Permissions.Company.Members.Manage));
                break;
            case Access.Business:
                Assert.True(context.Can(Permissions.Users.Manage));
                Assert.False(context.Can(PlatformPermissions.Recoveries.Manage));
                Assert.Equal([CompanyId], context.CompaniesWhere(Permissions.Company.Members.Manage));
                break;
            default:
                Assert.Equal([PlatformPermissions.Recoveries.Manage], context.Permissions);
                Assert.Empty(context.CompaniesWhere(Permissions.Company.Members.Manage));
                break;
        }
    }

    [Fact]
    public async Task The_bell_lists_the_fifty_newest_of_every_source_newest_first()
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var nowUtc = fixture.TimeProvider.GetUtcNow().UtcDateTime;
        var even = new ProbeSource([.. Enumerable.Range(0, 30).Select(index => Item($"even:{index * 2}", nowUtc.AddMinutes(-index * 2)))]);
        var odd = new ProbeSource([.. Enumerable.Range(0, 30).Select(index => Item($"odd:{index * 2 + 1}", nowUtc.AddMinutes(-index * 2 - 1)))]);
        var service = Service(fixture, [even, odd], Access.Consumer);

        var result = await service.ListAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(NotificationListResponse.Limit, result.Value.Items.Count);
        Assert.Equal(Enumerable.Range(0, 50).Select(minute => minute % 2 == 0 ? $"even:{minute}" : $"odd:{minute}"),
            result.Value.Items.Select(item => item.Id));
        Assert.Equal(50, result.Value.Unread);
        Assert.All(result.Value.Items, item => Assert.False(item.Read));
    }

    [Fact]
    public async Task A_failing_source_does_not_take_down_the_others()
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var nowUtc = fixture.TimeProvider.GetUtcNow().UtcDateTime;
        var service = Service(fixture, [new FailingSource(), new ProbeSource([Item("ok:1", nowUtc)])], Access.Business);

        var result = await service.ListAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["ok:1"], result.Value.Items.Select(item => item.Id));
        var failure = Assert.Single(fixture.Logger.Collector.GetSnapshot(), log => log.Level == LogLevel.Error);
        Assert.Contains(nameof(InvalidOperationException), failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("long")]
    [InlineData("slash")]
    [InlineData("local")]
    [InlineData("link")]
    public async Task A_source_that_breaks_the_contract_is_left_out_whole(string breach)
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var nowUtc = fixture.TimeProvider.GetUtcNow().UtcDateTime;
        var item = breach switch
        {
            "long" => Item(new string('x', NotificationItem.IdMaxLength + 1), nowUtc),
            "slash" => Item("broken:a/b", nowUtc),
            "local" => Item("broken:local", DateTime.SpecifyKind(nowUtc, DateTimeKind.Local)),
            _ => Item("broken:link", nowUtc) with { Link = "https://example.test" },
        };
        var broken = new ProbeSource([Item("broken:ok", nowUtc), item]);
        var service = Service(fixture, [broken, new ProbeSource([Item("ok:1", nowUtc.AddMinutes(-1))])], Access.Consumer);

        var result = await service.ListAsync(Ct);

        Assert.Equal(["ok:1"], result.Value.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Read_notices_come_marked_and_unread_counts_the_others()
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var nowUtc = fixture.TimeProvider.GetUtcNow().UtcDateTime;
        var reads = new FakeReads();
        var service = Service(fixture, [new ProbeSource([Item("a", nowUtc), Item("b", nowUtc.AddMinutes(-1)),
            Item("c", nowUtc.AddMinutes(-2))])], Access.Consumer, reads);
        reads.Mark(UserId, "b");
        reads.Mark(Guid.NewGuid(), "a");

        var result = await service.ListAsync(Ct);

        Assert.Equal([false, true, false], result.Value.Items.Select(item => item.Read));
        Assert.Equal(2, result.Value.Unread);
    }

    [Fact]
    public async Task Marking_a_visible_notice_saves_it_once_with_the_account_and_the_moment()
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var nowUtc = fixture.TimeProvider.GetUtcNow().UtcDateTime;
        var reads = new FakeReads();
        var unitOfWork = new FakeUnitOfWork();
        var service = Service(fixture, [new ProbeSource([Item("a", nowUtc)])], Access.Platform, reads, unitOfWork);

        Assert.True((await service.MarkReadAsync("a", Ct)).IsSuccess);
        Assert.True((await service.MarkReadAsync("a", Ct)).IsSuccess);

        var saved = Assert.Single(reads.Added);
        Assert.Equal((UserId, "a", nowUtc), (saved.UserId, saved.NotificationId, saved.ReadAtUtc));
        Assert.Equal(2, unitOfWork.Transactions);
        Assert.Equal(CommitPolicy.OnSuccess, unitOfWork.LastPolicy);
    }

    [Theory]
    [InlineData("other")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_id_that_is_not_a_visible_notice_is_ignored_without_saving(string id)
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var reads = new FakeReads();
        var unitOfWork = new FakeUnitOfWork();
        var service = Service(fixture, [new ProbeSource([Item("a", fixture.TimeProvider.GetUtcNow().UtcDateTime)])],
            Access.Consumer, reads, unitOfWork);

        var result = await service.MarkReadAsync(id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(reads.Added);
        Assert.Equal(0, unitOfWork.Transactions);
    }

    [Fact]
    public async Task A_notice_beyond_the_fifty_shown_is_not_visible()
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var nowUtc = fixture.TimeProvider.GetUtcNow().UtcDateTime;
        var reads = new FakeReads();
        var service = Service(fixture, [new ProbeSource([.. Enumerable.Range(0, 51)
            .Select(index => Item($"n:{index}", nowUtc.AddMinutes(-index)))])], Access.Consumer, reads);

        Assert.True((await service.MarkReadAsync("n:50", Ct)).IsSuccess);
        Assert.True((await service.MarkReadAsync("n:49", Ct)).IsSuccess);

        Assert.Equal(["n:49"], reads.Added.Select(read => read.NotificationId));
    }

    // Revisión de la base de la 12 (punto 11): una lectura de cada proyección por pedido, como la autorización (B10).
    [Fact]
    public async Task Business_reads_each_permission_projection_once_per_request()
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var cache = new CountingPermissionCache([Permissions.Users.Manage],
            new Dictionary<Guid, IReadOnlyList<string>> { [CompanyId] = [Permissions.Company.Members.Manage] });
        var tenant = new FakeTenantContext { TenantId = Guid.NewGuid(), TenantKind = TenantKind.Business };
        var source = new ProbeSource([Item("a", fixture.TimeProvider.GetUtcNow().UtcDateTime)]);
        var companies = new CompanyActivityReader();
        var service = Service(fixture, [source], Access.Business, permissions: new PermissionService(cache, tenant,
            companies, fixture.TimeProvider, NullLogger<PermissionService>.Instance));

        Assert.True((await service.ListAsync(Ct)).IsSuccess);
        Assert.Equal((1, 1), (cache.OrganizationReads, cache.CompanyReads));
        Assert.True((await service.MarkReadAsync("a", Ct)).IsSuccess);
        Assert.Equal((2, 2), (cache.OrganizationReads, cache.CompanyReads));
        // La campana no pregunta por una empresa suelta: lee las empresas desde la proyección.
        Assert.Equal(0, companies.Checks);

        // Las dos vistas no cambian: la organización con lo suyo y cada empresa con lo de la organización incluido.
        Assert.All(source.Contexts, context =>
        {
            Assert.Equal([Permissions.Users.Manage], context.Permissions);
            Assert.Equal([CompanyId], context.CompaniesWhere(Permissions.Company.Members.Manage));
            Assert.Equal([CompanyId], context.CompaniesWhere(Permissions.Users.Manage));
        });
    }

    [Fact]
    public async Task Two_marks_at_once_save_one_and_both_answer_success()
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var unitOfWork = new FakeUnitOfWork
        {
            CommitFailure = new UniqueConstraintViolationException { ConstraintName = UniqueKeys.NotificationRead },
        };
        var service = Service(fixture, [new ProbeSource([Item("a", fixture.TimeProvider.GetUtcNow().UtcDateTime)])],
            Access.Business, new FakeReads(), unitOfWork);

        var result = await service.MarkReadAsync("a", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task Without_a_signed_access_the_bell_answers_forbidden_and_reads_nothing()
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var source = new ProbeSource();
        var service = new NotificationService([source], new FakeReads(), new FakeReads(), new FakePermissions(),
            new FakePlatformPermissions(), new CurrentUser(null, null), new FakeUnitOfWork(), fixture.TimeProvider,
            fixture.Logger);

        var list = await service.ListAsync(Ct);
        var mark = await service.MarkReadAsync("a", Ct);

        Assert.Equal(AccessErrors.Wrong, list.Error);
        Assert.Equal(AccessErrors.Wrong, mark.Error);
        Assert.Empty(source.Contexts);
    }

    [Fact]
    public async Task The_cleanup_removes_a_batch_of_reads_older_than_ninety_days_in_one_transaction()
    {
        using var fixture = new ServiceFixture<NotificationService>();
        var reads = new FakeReads();
        var unitOfWork = new FakeUnitOfWork();
        var service = new NotificationService([], reads, reads, new FakePermissions(), new FakePlatformPermissions(),
            new CurrentUser(null, null), unitOfWork, fixture.TimeProvider, fixture.Logger);

        var result = await service.CleanupExpiredReadsAsync(Ct);

        Assert.Equal(7, result.Value);
        Assert.Equal((fixture.TimeProvider.GetUtcNow().UtcDateTime.AddDays(-90), NotificationService.CleanupBatchSize),
            Assert.Single(reads.Expirations));
        Assert.Equal((1, 1), (unitOfWork.Transactions, unitOfWork.Commits));
    }

    [Fact]
    public async Task The_account_deletion_removes_its_reads_only_in_the_global_step()
    {
        var reads = new FakeReads();
        var participant = new NotificationReadDeletionParticipant(reads);
        var userId = Guid.NewGuid();
        var nowUtc = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

        await participant.ExecuteAsync(new(userId, nowUtc, Guid.NewGuid(), TenantKind.Business), Ct);
        await participant.ExecuteAsync(new(userId, nowUtc, Guid.NewGuid(), TenantKind.Personal), Ct);
        Assert.Empty(reads.RemovedAccounts);

        await participant.ExecuteAsync(new(userId, nowUtc), Ct);
        Assert.Equal([userId], reads.RemovedAccounts);
    }

    private static readonly Guid UserId = Guid.NewGuid();

    private static NotificationService Service(ServiceFixture<NotificationService> fixture,
        IEnumerable<INotificationSource> sources, Access access, FakeReads? reads = null, FakeUnitOfWork? unitOfWork = null,
        IPermissionService? permissions = null)
    {
        reads ??= new FakeReads();
        return new NotificationService(sources, reads, reads, permissions ?? new FakePermissions(), new FakePlatformPermissions(),
            new CurrentUser(UserId, access), unitOfWork ?? new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);
    }

    private static NotificationItem Item(string id, DateTime occurredAtUtc) =>
        new(id, "probe", new Dictionary<string, string>(), occurredAtUtc, "/", Read: false);

    private sealed class ProbeSource(IReadOnlyList<NotificationItem>? items = null) : INotificationSource
    {
        public List<NotificationContext> Contexts { get; } = [];

        public Task<IReadOnlyList<NotificationItem>> ListAsync(NotificationContext context, CancellationToken cancellationToken)
        {
            Contexts.Add(context);
            return Task.FromResult(items ?? []);
        }
    }

    private sealed class FailingSource : INotificationSource
    {
        public Task<IReadOnlyList<NotificationItem>> ListAsync(NotificationContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("secret");
    }

    private sealed class FakeReads : INotificationReadReader, INotificationReadRepository
    {
        private readonly HashSet<(Guid, string)> _read = [];

        public List<NotificationRead> Added { get; } = [];

        public void Mark(Guid userId, string id) => _read.Add((userId, id));

        public Task<IReadOnlyList<string>> ListReadIdsAsync(Guid userId, IReadOnlyCollection<string> notificationIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([.. notificationIds.Where(id => _read.Contains((userId, id)))]);

        public Task<bool> ExistsAsync(Guid userId, string notificationId, CancellationToken cancellationToken) =>
            Task.FromResult(_read.Contains((userId, notificationId)));

        public void Add(NotificationRead read)
        {
            Added.Add(read);
            _read.Add((read.UserId, read.NotificationId));
        }

        public List<Guid> RemovedAccounts { get; } = [];

        public List<(DateTime BeforeUtc, int BatchSize)> Expirations { get; } = [];

        public Task RemoveForAccountAsync(Guid userId, CancellationToken cancellationToken)
        {
            RemovedAccounts.Add(userId);
            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken)
        {
            Expirations.Add((beforeUtc, batchSize));
            return Task.FromResult(7);
        }
    }

    private sealed class FakePermissions : IPermissionService
    {
        public Task<IReadOnlyList<string>> GetAssignedOrganizationPermissionsAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([Permissions.Users.Manage]);
        public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId, CancellationToken ct) =>
            throw new InvalidOperationException("The bell reads the projections once.");
        public Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId,
            CancellationToken ct) => throw new InvalidOperationException("The bell reads the projections once.");
        public Task<BusinessPermissions> GetBusinessPermissionsAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(new BusinessPermissions([Permissions.Users.Manage],
                new Dictionary<Guid, IReadOnlyList<string>> { [CompanyId] = [Permissions.Company.Members.Manage] },
                [Permissions.Users.Manage]));
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct) =>
            throw new InvalidOperationException("The bell reads the projections once.");
        public Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission, CancellationToken ct) =>
            throw new InvalidOperationException("The bell reads the projections once.");
        public Task<bool> HasCompanyPermissionAsync(BusinessPermissions granted, Guid companyId, string permission,
            CancellationToken ct) => throw new InvalidOperationException("The bell reads the projections once.");
        public Task<bool> HasCompanyRequirementAsync(Guid userId, Guid companyId, string permission,
            string? organizationPermission, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePlatformPermissions : IPlatformPermissionService
    {
        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([PlatformPermissions.Recoveries.Manage]);
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The bell reads the platform permissions once.");
    }

    private sealed class CurrentUser(Guid? userId, Access? access) : ICurrentUser
    {
        public Guid? UserId => userId;
        public Access? Access => access;
    }
}
