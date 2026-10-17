using System.Globalization;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Legal;

/// <summary>El worker, la UoW, el contexto y RLS reales conservan el registro empresarial y eliminan sólo la copia personal.</summary>
public sealed partial class AccountDeletionTests
{
    [Fact]
    public async Task Deleting_the_account_replaces_the_shared_copy_and_the_business_keeps_its_row()
    {
        await using var setup = await SharedDeletionSetup.CreateAsync(factory);
        var id = await setup.AddDealAsync();
        await setup.DeleteAsync();
        var saved = await setup.ReadDealAsync(id);
        Assert.Equal(("Resumen de negocio", DealStatus.Confirmed), (saved.Summary, saved.Status));
        Assert.Equal(FormattingTexts.DeletedAccount, saved.ConsumerName);
        var stored = await setup.StoredDealAsync(id);
        Assert.Null(stored.ConsumerName);
        Assert.True(stored.ConsumerCopyDeleted);
        await setup.AssertClosedAsync();
        await setup.AssertNoPersonalAuditAsync();
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            Assert.Equal(FormattingTexts.DeletedAccount, (await setup.ReadDealAsync(id)).ConsumerName);
        }
        finally { CultureInfo.CurrentUICulture = previousCulture; }
    }

    [Fact]
    public async Task Repeating_the_initial_retained_participant_in_one_transaction_fixes_one_deadline()
    {
        await using var setup = await SharedDeletionSetup.CreateAsync(factory,
            new SharedRetentionPolicy(typeof(Deal), TimeSpan.FromDays(10)));
        var id = await setup.AddDealAsync();
        var deadline = setup.Clock.GetUtcNow().UtcDateTime.AddDays(10);
        await setup.RepeatParticipantAsync();
        Assert.Equal(deadline, Assert.Single(await setup.RetentionsAsync()).RetainUntilUtc);
        Assert.Equal(EngagementJourney.CopiedName, (await setup.ReadDealAsync(id)).ConsumerName);
        await setup.DeleteAsync();
        Assert.Equal(deadline, Assert.Single(await setup.RetentionsAsync()).RetainUntilUtc);
        setup.Clock.Advance(TimeSpan.FromDays(10) - TimeSpan.FromTicks(1));
        await setup.RunWorkerAsync();
        Assert.Equal(EngagementJourney.CopiedName, (await setup.ReadDealAsync(id)).ConsumerName);
        setup.Clock.Advance(TimeSpan.FromTicks(1));
        await setup.RunWorkerAsync();
        Assert.True((await setup.StoredDealAsync(id)).ConsumerCopyDeleted);
        var completed = Assert.Single(await setup.RetentionsAsync());
        Assert.Equal(deadline, completed.RetainUntilUtc);
        Assert.Equal(deadline, completed.CopyReplacedAtUtc);
    }

    [Fact]
    public async Task A_retained_shared_copy_is_replaced_only_when_its_period_ends()
    {
        await using var setup = await SharedDeletionSetup.CreateAsync(factory,
            new SharedRetentionPolicy(typeof(Deal), TimeSpan.FromDays(10)));
        var id = await setup.AddDealAsync();
        await setup.DeleteAsync();
        var retention = Assert.Single(await setup.RetentionsAsync());
        Assert.Equal("engagement.Deals", retention.EntityTypeKey);
        Assert.Equal(setup.Clock.GetUtcNow().UtcDateTime.AddDays(10), retention.RetainUntilUtc);
        Assert.Equal(EngagementJourney.CopiedName, (await setup.ReadDealAsync(id)).ConsumerName);
        setup.Clock.Advance(TimeSpan.FromDays(2));
        await setup.RepeatParticipantAsync();
        Assert.Equal(retention.RetainUntilUtc, Assert.Single(await setup.RetentionsAsync()).RetainUntilUtc);
        setup.Clock.Advance(TimeSpan.FromDays(8) - TimeSpan.FromTicks(1));
        await setup.RunWorkerAsync();
        Assert.Equal(EngagementJourney.CopiedName, (await setup.ReadDealAsync(id)).ConsumerName);
        setup.Clock.Advance(TimeSpan.FromTicks(1));
        await setup.RunWorkerAsync();
        Assert.True((await setup.StoredDealAsync(id)).ConsumerCopyDeleted);
        Assert.Equal(retention.RetainUntilUtc, Assert.Single(await setup.RetentionsAsync()).CopyReplacedAtUtc);
        setup.Clock.Advance(TimeSpan.FromDays(1));
        await setup.RepeatParticipantAsync();
        await setup.RunWorkerAsync();
        var repeated = Assert.Single(await setup.RetentionsAsync());
        Assert.Equal(retention.RetainUntilUtc, repeated.RetainUntilUtc);
        Assert.Equal(retention.RetainUntilUtc, repeated.CopyReplacedAtUtc);
        await setup.AssertNoPersonalAuditAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deleting_one_account_leaves_the_shared_data_of_others_intact(bool retained)
    {
        var policies = retained ? new[] { new SharedRetentionPolicy(typeof(Deal), TimeSpan.FromDays(1)) } : [];
        await using var setup = await SharedDeletionSetup.CreateAsync(factory, policies);
        using var other = await EngagementJourney.CreateAsync(setup.Factory);
        var ownId = await setup.AddDealAsync();
        var otherId = await setup.AddDealAsync(other.PersonalId, setup.Journey.BusinessA.Id, "Otra persona");
        await setup.DeleteAsync();
        if (retained)
        {
            setup.Clock.Advance(TimeSpan.FromDays(1));
            await setup.RunWorkerAsync();
        }
        Assert.Null((await setup.StoredDealAsync(ownId)).ConsumerName);
        var untouched = await setup.StoredDealAsync(otherId);
        Assert.Equal("Otra persona", untouched.ConsumerName);
        Assert.False(untouched.ConsumerCopyDeleted);
        Assert.Equal(DealStatus.Confirmed, untouched.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_fixed_retention_survives_a_changed_or_removed_module_policy(bool removed)
    {
        await using var setup = await SharedDeletionSetup.CreateAsync(factory,
            new SharedRetentionPolicy(typeof(Deal), TimeSpan.FromDays(10)));
        var id = await setup.AddDealAsync();
        await setup.DeleteAsync();
        var original = Assert.Single(await setup.RetentionsAsync());
        setup.Clock.Advance(TimeSpan.FromDays(2));
        await using var changed = setup.Factory.Host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRetainedOnConsumerDeletion>();
            if (!removed) services.AddSingleton<IRetainedOnConsumerDeletion>(
                new SharedRetentionPolicy(typeof(Deal), TimeSpan.FromDays(1), "Test.ChangedRetention"));
        }));
        await using var scope = changed.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(setup.Journey.PersonalId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            await services.GetServices<IAccountDeletionParticipant>().Single(row => row.GetType().Name == nameof(EngagementDeletionParticipant))
                .ExecuteAsync(new AccountDeletionContext(setup.Journey.Account.UserId, setup.Clock.GetUtcNow().UtcDateTime,
                    setup.Journey.PersonalId, TenantKind.Personal), ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.Equal(EngagementJourney.CopiedName, (await setup.ReadDealAsync(id)).ConsumerName);
        var saved = Assert.Single(await setup.RetentionsAsync());
        Assert.Equal((original.RetainUntilUtc, original.Reason), (saved.RetainUntilUtc, saved.Reason));
        setup.Clock.Advance(TimeSpan.FromDays(8));
        await setup.RunWorkerAsync();
        Assert.True((await setup.StoredDealAsync(id)).ConsumerCopyDeleted);
    }

    [Fact]
    public async Task An_absent_original_name_is_distinct_from_a_deleted_copy()
    {
        await using var setup = await SharedDeletionSetup.CreateAsync(factory);
        var id = await setup.AddDealAsync(consumerName: null);
        Assert.Null((await setup.ReadDealAsync(id)).ConsumerName);
        Assert.False((await setup.StoredDealAsync(id)).ConsumerCopyDeleted);
        await setup.DeleteAsync();
        Assert.Equal(FormattingTexts.DeletedAccount, (await setup.ReadDealAsync(id)).ConsumerName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_soft_deleted_shared_row_also_loses_every_personal_copy(bool retained)
    {
        var policies = retained ? new[] { new SharedRetentionPolicy(typeof(RetainedSharedRecord), TimeSpan.FromDays(1)) } : [];
        await using var setup = await SharedDeletionSetup.CreateAsync(factory, policies);
        var id = await setup.AddSoftDeletedAsync();
        await setup.DeleteAsync();
        if (retained)
        {
            var before = await setup.StoredSoftDeletedAsync(id);
            Assert.Equal(EngagementJourney.CopiedName, before.ConsumerName);
            Assert.Equal(SharedDeletionSetup.CopiedNote, before.ConsumerNote);
            setup.Clock.Advance(TimeSpan.FromDays(1));
            await setup.RunWorkerAsync();
        }
        var saved = await setup.StoredSoftDeletedAsync(id);
        Assert.True(saved.IsDeleted);
        Assert.NotNull(saved.DeletedAtUtc);
        Assert.Null(saved.ConsumerName);
        Assert.Null(saved.ConsumerNote);
        Assert.True(saved.ConsumerCopyDeleted);
        Assert.Equal("Resumen de negocio conservado", saved.Summary);
        await setup.AssertNoPersonalAuditAsync();
    }

    [Fact]
    public async Task Retention_processing_skips_a_locked_row_and_retries_it_once()
    {
        await using var setup = await SharedDeletionSetup.CreateAsync(factory,
            new SharedRetentionPolicy(typeof(Deal), TimeSpan.FromDays(1)));
        var id = await setup.AddDealAsync();
        await setup.DeleteAsync();
        setup.Clock.Advance(TimeSpan.FromDays(1));
        var retention = Assert.Single(await setup.RetentionsAsync());
        var work = new ConsumerDataRetentionWork(retention.Id, setup.Journey.PersonalId);
        var locked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var holding = HoldRetentionAsync();
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            await setup.ProcessRetentionAsync(work);
            Assert.False((await setup.StoredDealAsync(id)).ConsumerCopyDeleted);
        }
        finally { release.TrySetResult(true); await holding; }
        await setup.ProcessRetentionAsync(work);
        await setup.ProcessRetentionAsync(work);
        Assert.True((await setup.StoredDealAsync(id)).ConsumerCopyDeleted);
        Assert.Equal(setup.Clock.GetUtcNow().UtcDateTime, Assert.Single(await setup.RetentionsAsync()).CopyReplacedAtUtc);

        async Task HoldRetentionAsync()
        {
            await using var scope = setup.Factory.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(work.ConsumerTenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                Assert.NotNull(await services.GetRequiredService<IConsumerDataRetentionRepository>()
                    .GetForProcessingAsync(work, setup.Clock.GetUtcNow().UtcDateTime, ct));
                locked.TrySetResult(true);
                await release.Task.WaitAsync(ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
    }

    [Fact]
    public async Task Each_worker_run_replaces_at_most_one_hundred_expired_copies()
    {
        await using var setup = await SharedDeletionSetup.CreateAsync(factory,
            new SharedRetentionPolicy(typeof(Deal), TimeSpan.FromDays(1)));
        await setup.AddDealsAsync(101);
        await setup.DeleteAsync();
        setup.Clock.Advance(TimeSpan.FromDays(1));
        await setup.RunWorkerAsync();
        var first = await setup.RetentionsAsync();
        Assert.Equal(100, first.Count(row => row.CopyReplacedAtUtc is not null));
        Assert.Single(first, row => row.CopyReplacedAtUtc is null);
        await setup.RunWorkerAsync();
        Assert.Equal(101, (await setup.RetentionsAsync()).Count(row => row.CopyReplacedAtUtc is not null));
    }

    [Fact]
    public async Task Retention_policies_reject_invalid_period_reason_type_and_duplicate_types_as_configuration_bugs()
    {
        await using var setup = await SharedDeletionSetup.CreateAsync(factory);
        await using var scope = setup.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<ApplicationDbContext>();
        var tenant = services.GetRequiredService<ITenantContext>();
        foreach (var policy in new[]
        {
            new SharedRetentionPolicy(typeof(Deal), TimeSpan.Zero),
            new SharedRetentionPolicy(typeof(Widget), TimeSpan.FromDays(1)),
            new SharedRetentionPolicy(typeof(Deal), TimeSpan.FromDays(1), "Un motivo con datos personales"),
        }) Assert.Throws<InvalidOperationException>(() => new EngagementCopyDeletion(database, tenant, [policy]));
        var valid = new SharedRetentionPolicy(typeof(Deal), TimeSpan.FromDays(1));
        Assert.Throws<InvalidOperationException>(() => new EngagementCopyDeletion(database, tenant, [valid, valid]));
    }

    private sealed record SharedRetentionPolicy(Type EntityType, TimeSpan RetentionPeriod,
        string Reason = "Test.LegalRetention") : IRetainedOnConsumerDeletion;

    private sealed class SharedDeletionSetup(EngagementFactory engagement, EngagementJourney journey,
        FakeTimeProvider clock) : IAsyncDisposable
    {
        internal const string CopiedNote = "Dato personal copiado para probar su borrado";
        internal EngagementFactory Factory => engagement;
        internal EngagementJourney Journey => journey;
        internal FakeTimeProvider Clock => clock;

        internal static async Task<SharedDeletionSetup> CreateAsync(ApiFactory database, params SharedRetentionPolicy[] policies)
        {
            var now = database.Services.GetRequiredService<TimeProvider>().GetUtcNow();
            var clock = new FakeTimeProvider(now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)));
            var engagement = new EngagementFactory(database)
            {
                ConfigureServices = services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                    foreach (var policy in policies) services.AddSingleton<IRetainedOnConsumerDeletion>(policy);
                },
            };
            try
            {
                await engagement.InitializeAsync();
                return new SharedDeletionSetup(engagement, await EngagementJourney.CreateAsync(engagement), clock);
            }
            catch { await engagement.DisposeAsync(); throw; }
        }

        internal Task<Guid> AddDealAsync(string? consumerName = EngagementJourney.CopiedName) =>
            AddDealAsync(journey.PersonalId, journey.BusinessA.Id, consumerName);

        internal async Task<Guid> AddDealAsync(Guid consumerId, Guid businessId, string? consumerName)
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(consumerId, TenantKind.Personal);
            var deal = Deal.Request(businessId, "Resumen de negocio", consumerName);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<ApplicationDbContext>().Set<Deal>().Add(deal);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            // Conservar un estado empresarial distinto permite detectar un barrido destructivo.
            await using var businessScope = engagement.Services.CreateAsyncScope();
            var business = businessScope.ServiceProvider;
            using var tenant = business.GetRequiredService<ITenantScope>().Enter(businessId);
            await business.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var saved = await business.GetRequiredService<ApplicationDbContext>().Set<Deal>().SingleAsync(row => row.Id == deal.Id, ct);
                Assert.True(saved.Transition(DealStatus.Confirmed, Party.Business, businessId).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            return deal.Id;
        }

        internal async Task AddDealsAsync(int count)
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(journey.PersonalId, TenantKind.Personal);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<ApplicationDbContext>().Set<Deal>().AddRange(Enumerable.Range(0, count)
                    .Select(_ => Deal.Request(journey.BusinessA.Id, "Resumen de negocio", EngagementJourney.CopiedName)));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        internal async Task<Guid> AddSoftDeletedAsync()
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(journey.PersonalId, TenantKind.Personal);
            var database = services.GetRequiredService<ApplicationDbContext>();
            var record = new RetainedSharedRecord(journey.BusinessA.Id, EngagementJourney.CopiedName, CopiedNote);
            var unit = services.GetRequiredService<IUnitOfWork>();
            await unit.ExecuteInTransactionAsync(ct =>
            {
                database.Set<RetainedSharedRecord>().Add(record);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            await unit.ExecuteInTransactionAsync(ct =>
            {
                database.Set<RetainedSharedRecord>().Remove(record);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(record.IsDeleted);
            Assert.False(await database.Set<RetainedSharedRecord>().AnyAsync(row => row.Id == record.Id, Ct));
            return record.Id;
        }

        internal async Task DeleteAsync()
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var account = await services.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(row => row.Id == journey.Account.UserId, ct);
                Assert.True(account.RequestDeletion("Preparación de prueba de baja", clock.GetUtcNow().UtcDateTime.AddDays(-31), 30).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            await RunWorkerAsync();
            await AssertClosedAsync();
        }

        internal Task RunWorkerAsync() => ActivatorUtilities.CreateInstance<AccountDeletionWorker>(engagement.Services).RunOnceAsync(Ct);

        internal async Task RepeatParticipantAsync()
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(journey.PersonalId);
            var participant = Assert.Single(services.GetServices<IAccountDeletionParticipant>(),
                row => row.GetType().Name == nameof(EngagementDeletionParticipant));
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var context = new AccountDeletionContext(journey.Account.UserId, clock.GetUtcNow().UtcDateTime,
                    journey.PersonalId, TenantKind.Personal);
                await participant.ExecuteAsync(context, ct);
                await participant.ExecuteAsync(context, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        internal async Task ProcessRetentionAsync(ConsumerDataRetentionWork work)
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>()
                .ProcessRetentionAsync(work, Ct)).IsSuccess);
        }

        internal async Task<IReadOnlyList<ConsumerDataRetention>> RetentionsAsync()
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ConsumerDataRetentions.AsNoTracking()
                .Where(row => row.ConsumerTenantId == journey.PersonalId).OrderBy(row => row.Id).ToArrayAsync(Ct);
        }

        internal async Task<DealResponse> ReadDealAsync(Guid id)
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(journey.BusinessA.Id);
            return (await scope.ServiceProvider.GetRequiredService<IDealReader>().FindAsync(id, Ct))!;
        }

        internal async Task<Deal> StoredDealAsync(Guid id)
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(journey.BusinessA.Id);
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<Deal>().AsNoTracking()
                .SingleAsync(row => row.Id == id, Ct);
        }

        internal async Task<RetainedSharedRecord> StoredSoftDeletedAsync(Guid id)
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(journey.BusinessA.Id);
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<RetainedSharedRecord>()
                .IgnoreQueryFilters(["SoftDelete"]).AsNoTracking().SingleAsync(row => row.Id == id, Ct);
        }

        internal async Task AssertClosedAsync()
        {
            await using var scope = engagement.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(UserStatus.Deleted, (await database.Users.AsNoTracking().SingleAsync(row => row.Id == journey.Account.UserId, Ct)).Status);
            Assert.Equal(TenantStatus.Closed, (await database.Tenants.AsNoTracking().SingleAsync(row => row.Id == journey.PersonalId, Ct)).Status);
        }

        internal async Task AssertNoPersonalAuditAsync()
        {
            foreach (var tenantId in new[] { journey.PersonalId, journey.BusinessA.Id })
            {
                await using var scope = engagement.Services.CreateAsyncScope();
                using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
                var changes = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AuditEntry>()
                    .AsNoTracking().Where(row => row.EntityType == nameof(Deal) || row.EntityType == nameof(RetainedSharedRecord))
                    .Select(row => row.Changes).ToArrayAsync(Ct);
                Assert.All(changes, change =>
                {
                    Assert.DoesNotContain(EngagementJourney.CopiedName, change, StringComparison.Ordinal);
                    Assert.DoesNotContain(CopiedNote, change, StringComparison.Ordinal);
                });
            }
        }

        public async ValueTask DisposeAsync()
        {
            journey.Dispose();
            await engagement.DisposeAsync();
        }
    }
}
