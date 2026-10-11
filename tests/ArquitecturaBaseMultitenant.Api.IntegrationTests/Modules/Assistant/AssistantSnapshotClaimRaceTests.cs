using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantSnapshotClaimRaceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Claim_after_composition_prevents_a_late_personal_copy_even_when_the_lease_has_expired(bool platform, bool expiredLease)
    {
        // Claim has no account filter: this database contains exactly one due identity.
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var phases = new RacePhases();
        using var host = Host(factory, phases);
        using var ownerClient = Client(host);
        using var targetClient = Client(host);
        var data = await SetupAsync(factory, host, ownerClient, targetClient, platform);
        phases.CompositionArmed = true;
        var key = Guid.CreateVersion7();
        var pending = AssistantDurableConversationTests.PostAsync(ownerClient, data.Route, "{}", key);
        AccountDeletionWork? work = null;
        try
        {
            await phases.Composed.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            AssertPrepared(phases, data);
            await RequestAndVerifyGraceAsync(host, ownerClient, targetClient, data);
            await MakeDueAsync(host.Services, data.TargetId);
            work = await ClaimAsync(host.Services, data.TargetId, expiredLease);
            Assert.NotNull(work);
            var identity = await IdentityAsync(host.Services, data.TargetId);
            Assert.Equal(work.LeaseId, identity.DeletionLeaseId);
            Assert.Equal(expiredLease, identity.DeletionLeaseExpiresAtUtc <= data.NowUtc);
            using var hidden = await AssistantDurableConversationTests.PostAsync(ownerClient, data.Route, "{}", data.OriginalKey);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            var original = await ReadBindingAsync(host.Services, data, data.OriginalKey);
            Assert.Contains(data.TargetName, original.SnapshotJson!, StringComparison.Ordinal);
            Assert.Contains(data.TargetId, original.Subjects);
        }
        finally { phases.ResumeComposition.TrySetResult(); }
        using var completed = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var body = await completed.Content.ReadAsStringAsync(Ct);
        Assert.Null(NameOf(body, data));
        Assert.DoesNotContain(data.TargetName, body, StringComparison.Ordinal);
        Assert.Equal(phases.Prepared!.Result.Text, TextOf(body));
        Assert.Equal(1, phases.PausedCompositions);
        var safeCopy = await ReadBindingAsync(host.Services, data, key);
        Assert.NotNull(safeCopy.SnapshotJson);
        Assert.DoesNotContain(data.TargetName, safeCopy.SnapshotJson!, StringComparison.Ordinal);
        Assert.DoesNotContain(data.TargetId, safeCopy.Subjects);
        if (expiredLease)
        {
            var renewed = await ClaimAsync(host.Services, data.TargetId, expiredLease: false);
            Assert.NotNull(renewed);
            Assert.NotEqual(work!.LeaseId, renewed.LeaseId);
            work = renewed;
        }
        await ProcessAsync(host.Services, work!);
        await AssertPurgedOriginalAndDeletedOwnerBindingAsync(host.Services, data, data.OriginalKey);
        // The freshly protected copy has no target reference and survives that third party's deletion.
        Assert.NotNull((await ReadBindingAsync(host.Services, data, key)).SnapshotJson);
        using var replay = await AssistantDurableConversationTests.PostAsync(ownerClient, data.Route, "{}", data.OriginalKey);
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Writer_shares_make_claim_skip_the_subject_then_retry_after_commit_purges_the_whole_original(bool platform)
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var phases = new RacePhases();
        using var host = Host(factory, phases);
        using var ownerClient = Client(host);
        using var targetClient = Client(host);
        var data = await SetupAsync(factory, host, ownerClient, targetClient, platform);
        phases.CompositionArmed = true;
        phases.FenceArmed = true;
        var key = Guid.CreateVersion7();
        var pending = AssistantDurableConversationTests.PostAsync(ownerClient, data.Route, "{}", key);
        try
        {
            await phases.Composed.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            AssertPrepared(phases, data);
            await RequestAndVerifyGraceAsync(host, ownerClient, targetClient, data);
            await MakeDueAsync(host.Services, data.TargetId);
            phases.ResumeComposition.TrySetResult();
            await phases.Fenced.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            Assert.Equal(new[] { data.OwnerId, data.TargetId }.Order(), phases.FencedIds
                .Where(id => id == data.OwnerId || id == data.TargetId));
            Assert.Contains(data.OwnerId, phases.FencedIds);
            Assert.Contains(data.TargetId, phases.FencedIds);
            Assert.False(phases.FencedStates!.Single(row => row.UserId == data.TargetId).HasDeletionStarted);
            // SKIP LOCKED does not wait: the writer's shares make this sole due row ineligible until commit.
            Assert.Null(await ClaimAsync(host.Services, data.TargetId, expiredLease: false));
            Assert.Null((await IdentityAsync(host.Services, data.TargetId)).DeletionLeaseId);
        }
        finally
        {
            phases.ResumeComposition.TrySetResult();
            phases.ResumeFence.TrySetResult();
        }
        using var completed = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var body = await completed.Content.ReadAsStringAsync(Ct);
        Assert.Equal(data.TargetName, NameOf(body, data));
        var original = await ReadBindingAsync(host.Services, data, key);
        Assert.Contains(data.TargetName, original.SnapshotJson!, StringComparison.Ordinal);
        Assert.Contains(data.TargetId, original.Subjects);
        var work = await ClaimAsync(host.Services, data.TargetId, expiredLease: false);
        Assert.NotNull(work);
        Assert.Equal(work.LeaseId, (await IdentityAsync(host.Services, data.TargetId)).DeletionLeaseId);
        using (var beforeCleanup = await AssistantDurableConversationTests.PostAsync(ownerClient, data.Route, "{}", key))
            Assert.Equal(HttpStatusCode.NotFound, beforeCleanup.StatusCode);
        await ProcessAsync(host.Services, work);
        await AssertPurgedOriginalAndDeletedOwnerBindingAsync(host.Services, data, key);
        using var replay = await AssistantDurableConversationTests.PostAsync(ownerClient, data.Route, "{}", key);
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
        Assert.Equal(1, phases.PausedCompositions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Claim_during_the_last_durable_read_rejects_replay_without_purging_or_adapting_the_original(bool platform)
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var phases = new RacePhases();
        var pause = new ReplayReadPause();
        using var host = Host(factory, phases, pause);
        using var ownerClient = Client(host);
        using var targetClient = Client(host);
        var data = await SetupAsync(factory, host, ownerClient, targetClient, platform);
        await RequestAndVerifyGraceAsync(host, ownerClient, targetClient, data);
        await MakeDueAsync(host.Services, data.TargetId);
        var original = await ReadBindingAsync(host.Services, data, data.OriginalKey);
        Assert.Contains(data.TargetName, original.SnapshotJson!, StringComparison.Ordinal);
        Assert.Contains(data.TargetId, original.Subjects);
        var compositions = Volatile.Read(ref phases.TotalCompositions);
        pause.OwnerId = data.OwnerId;
        pause.OperationId = original.Id;
        pause.Armed = true;
        var pending = AssistantDurableConversationTests.PostAsync(ownerClient, data.Route, "{}", data.OriginalKey);
        try
        {
            await pause.SecondRead.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            Assert.Equal(2, pause.ObservedCalls);
            Assert.Equal(platform ? Access.Platform : Access.Business, pause.ObservedAccess);
            Assert.Equal(data.ScopeTenant, pause.ObservedTenant);
            Assert.NotNull(pause.InitialRow);
            Assert.Equal(original.SnapshotJson, pause.InitialRow.SnapshotJson);
            Assert.Contains(data.TargetId, pause.InitialRow.SubjectUserIds);
            Assert.Null((await IdentityAsync(host.Services, data.TargetId)).DeletionLeaseId);
            var work = await ClaimAsync(host.Services, data.TargetId, expiredLease: false);
            Assert.NotNull(work);
            var claimed = await IdentityAsync(host.Services, data.TargetId);
            Assert.Equal(work.LeaseId, claimed.DeletionLeaseId);
            Assert.Equal(UserStatus.PendingDeletion, claimed.Status);
            Assert.Equal(data.TargetName, claimed.DisplayName);
            // No participant has run. This await changed only the identity's claim signal.
            var stillOriginal = await ReadBindingAsync(host.Services, data, data.OriginalKey);
            Assert.Equal(original.SnapshotJson, stillOriginal.SnapshotJson);
            Assert.Equal(original.Subjects, stillOriginal.Subjects);
            Assert.Equal(original.QuickKey, stillOriginal.QuickKey);
        }
        finally { pause.Resume.TrySetResult(); }
        using var replay = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
        using var error = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct));
        Assert.Equal(AssistantErrors.ConversationNotFound.Code, error.RootElement.GetProperty("code").GetString());
        Assert.Equal(compositions, Volatile.Read(ref phases.TotalCompositions));
        Assert.Equal(2, pause.ObservedCalls);
        var afterRejection = await ReadBindingAsync(host.Services, data, data.OriginalKey);
        Assert.Equal(original.Id, afterRejection.Id);
        Assert.Equal(original.Key, afterRejection.Key);
        Assert.Equal(original.Route, afterRejection.Route);
        Assert.Equal(original.SnapshotJson, afterRejection.SnapshotJson);
        Assert.Equal(original.QuickKey, afterRejection.QuickKey);
        Assert.Equal(original.Subjects, afterRejection.Subjects);
    }

    private static WebApplicationFactory<Program> Host(ApiFactory factory, RacePhases phases, ReplayReadPause? replay = null) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Scoped<IAssistantQuickService>(provider => new PausedQuick(
                ActivatorUtilities.CreateInstance<AssistantQuickService>(provider), provider.GetRequiredService<ApplicationDbContext>(),
                provider.GetRequiredService<ICurrentUser>(), provider.GetRequiredService<ITenantContext>(), phases)));
            services.Replace(ServiceDescriptor.Scoped<IUserStatusReader>(provider => new PausedIdentityReader(
                ActivatorUtilities.CreateInstance<UserStatusReader>(provider), provider.GetRequiredService<ApplicationDbContext>(), phases)));
            if (replay is not null)
                services.Replace(ServiceDescriptor.Scoped<IAssistantDurableRepository>(provider => new PausedDurableRepository(
                    ActivatorUtilities.CreateInstance<AssistantDurableRepository>(provider), provider.GetRequiredService<ApplicationDbContext>(),
                    provider.GetRequiredService<ITenantContext>(), replay)));
        }));

    private static HttpClient Client(WebApplicationFactory<Program> host) => host.CreateClient(new()
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<RaceData> SetupAsync(ApiFactory factory, WebApplicationFactory<Program> host,
        HttpClient ownerClient, HttpClient targetClient, bool platform)
    {
        var target = await AccountJourney.RegisterAsync(host, targetClient, Ct);
        var personalTenant = await AssistantTestData.ActiveTenantAsync(targetClient);
        var owner = await AccountJourney.RegisterAsync(host, ownerClient, Ct);
        var businessTenant = await OrganizationJourney.StageAsync(factory, owner, "Claim race business", Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, businessTenant, target.UserId, Ct);
        Assert.NotEqual(target.UserId, memberId);
        var name = "Claim " + Guid.CreateVersion7().ToString("N")[..8];
        var nowUtc = host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var security = SecurityEvent.ForOrganizationMembershipEnded(target.UserId, businessTenant, owner.UserId, nowUtc);
        Assert.NotEqual(security.ActorId, security.TargetAccountId);
        using (var scope = host.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = provider.GetRequiredService<ApplicationDbContext>();
                Assert.True((await db.Users.SingleAsync(row => row.Id == target.UserId, ct)).Rename(name).IsSuccess);
                db.Add(security);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(targetClient);
        var targetOwnKey = Guid.CreateVersion7();
        using (var own = await AssistantDurableConversationTests.PostAsync(targetClient, "/api/me/assistant/quick/my_signin", "{}", targetOwnKey))
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        if (platform)
        {
            await PlatformOperators.GrantAsync(host.Services, owner.UserId, PlatformOperators.All, Ct);
            await AccountJourney.AuthorizeAsync(ownerClient, "platform", Ct);
        }
        else
        {
            await AssistantTestData.EnableAsync(factory, businessTenant);
            await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct, businessTenant);
        }
        var prefix = platform ? "/api/platform/assistant" : "/api/organization/assistant";
        await AssistantExecutionActorTests.AcceptNoticeAsync(ownerClient, prefix + "/notice");
        var route = prefix + "/quick/" + (platform ? "today_security" : "active_users");
        var originalKey = Guid.CreateVersion7();
        using var original = await AssistantDurableConversationTests.PostAsync(ownerClient, route, "{}", originalKey);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var body = await original.Content.ReadAsStringAsync(Ct);
        var data = new RaceData(platform, owner.UserId, target.UserId, platform ? null : businessTenant,
            personalTenant, platform ? security.Id : memberId, name, route, originalKey, targetOwnKey, body, nowUtc);
        Assert.Equal(name, NameOf(body, data));
        Assert.Contains(target.UserId, (await ReadBindingAsync(host.Services, data, originalKey)).Subjects);
        return data;
    }

    private static void AssertPrepared(RacePhases phases, RaceData data)
    {
        Assert.Equal(data.OwnerId, phases.PreparedOwner);
        Assert.Equal(data.ScopeTenant, phases.PreparedTenant);
        var copy = Assert.Single(phases.Prepared!.SubjectCopies, row => row.AccountId == data.TargetId && row.RowId == data.RowId);
        Assert.Equal(data.RowId, copy.RowId);
        Assert.Equal(data.Platform ? AssistantSnapshotCopyKind.SecurityActorDisplayName : AssistantSnapshotCopyKind.PeopleDisplayName, copy.CopyKind);
    }

    private static async Task RequestAndVerifyGraceAsync(WebApplicationFactory<Program> host, HttpClient ownerClient,
        HttpClient targetClient, RaceData data)
    {
        using var requested = await AccountJourney.PostAsync(targetClient, "/api/me/deletion", new { reason = "Claim race fixture" }, Ct);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var identity = await IdentityAsync(host.Services, data.TargetId);
        Assert.Equal(UserStatus.PendingDeletion, identity.Status);
        Assert.Null(identity.DeletionLeaseId);
        Assert.True(identity.DeletionScheduledForUtc > data.NowUtc);
        using var graceReplay = await AssistantDurableConversationTests.PostAsync(ownerClient, data.Route, "{}", data.OriginalKey);
        Assert.Equal(HttpStatusCode.OK, graceReplay.StatusCode);
        Assert.Equal(data.OriginalBody, await graceReplay.Content.ReadAsStringAsync(Ct));
    }

    private static async Task MakeDueAsync(IServiceProvider root, Guid targetId)
    {
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var target = await db.Users.SingleAsync(row => row.Id == targetId, ct);
            db.Entry(target).Property(row => row.DeletionScheduledForUtc).CurrentValue =
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(-1);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<AccountDeletionWork?> ClaimAsync(IServiceProvider root, Guid targetId, bool expiredLease)
    {
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        return (await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync<Result<AccountDeletionWork?>>(async ct =>
        {
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var candidates = await services.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking()
                .Where(row => row.DeletionScheduledForUtc <= now && (row.Status == UserStatus.PendingDeletion || row.Status == UserStatus.Suspended))
                .Select(row => row.Id).ToArrayAsync(ct);
            Assert.Equal(new[] { targetId }, candidates);
            var lease = Guid.CreateVersion7();
            var claimed = await services.GetRequiredService<IAccountDeletionRepository>().ClaimAsync(lease, now,
                now.AddMinutes(expiredLease ? -1 : 15), ct);
            if (claimed is null) return Result.Success<AccountDeletionWork?>(null);
            Assert.Equal(targetId, claimed.Value);
            return new AccountDeletionWork(claimed.Value, lease,
                await services.GetRequiredService<IAccountDeletionTenantReader>().ListAsync(claimed.Value, ct));
        }, CommitPolicy.OnSuccess, Ct)).Value;
    }

    private static async Task ProcessAsync(IServiceProvider root, AccountDeletionWork work)
    {
        foreach (var tenant in work.Tenants)
        {
            using var scope = root.CreateScope();
            var services = scope.ServiceProvider;
            Assert.Null(services.GetRequiredService<ITenantContext>().TenantId);
            var result = await services.GetRequiredService<IAccountDeletionProcessingService>().ProcessTenantAsync(work, tenant, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
            Assert.Null(services.GetRequiredService<ITenantContext>().TenantId);
        }
        using var final = root.CreateScope();
        var completed = await final.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().CompleteAsync(work, Ct);
        Assert.True(completed.IsSuccess, completed.Error.Code);
    }

    private static async Task AssertPurgedOriginalAndDeletedOwnerBindingAsync(IServiceProvider root, RaceData data, Guid key)
    {
        var original = await ReadBindingAsync(root, data, key);
        Assert.Null(original.SnapshotJson);
        Assert.Null(original.QuickKey);
        Assert.Empty(original.Subjects);
        Assert.Equal(key, original.Key);
        Assert.Equal(data.Route, original.Route);
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(UserStatus.Deleted, (await db.Users.AsNoTracking().SingleAsync(row => row.Id == data.TargetId, Ct)).Status);
        Assert.False(await db.Set<AssistantSnapshotRetentionIndex>().AnyAsync(row => row.OperationId == original.Id, Ct));
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(data.TargetPersonalTenant);
        Assert.False(await db.Set<AssistantDurableOperation>().AnyAsync(row => row.OwnerUserId == data.TargetId && row.Key == data.TargetOwnKey, Ct));
    }

    private static async Task<ApplicationUser> IdentityAsync(IServiceProvider root, Guid targetId)
    {
        using var scope = root.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking()
            .SingleAsync(row => row.Id == targetId, Ct);
    }

    private static async Task<Binding> ReadBindingAsync(IServiceProvider root, RaceData data, Guid key)
    {
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = data.ScopeTenant is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (data.Platform)
        {
            var row = await db.Set<PlatformAssistantDurableOperation>().AsNoTracking()
                .SingleAsync(row => row.OwnerUserId == data.OwnerId && row.Key == key, Ct);
            return new(row.Id, row.Key, row.Route, row.SnapshotJson, row.QuickKey, row.SubjectUserIds);
        }
        var privateRow = await db.Set<AssistantDurableOperation>().AsNoTracking()
            .SingleAsync(row => row.TenantId == data.ScopeTenant && row.OwnerUserId == data.OwnerId && row.Key == key, Ct);
        return new(privateRow.Id, privateRow.Key, privateRow.Route, privateRow.SnapshotJson, privateRow.QuickKey, privateRow.SubjectUserIds);
    }

    private static string? NameOf(string body, RaceData data)
    {
        using var json = JsonDocument.Parse(body);
        var rows = json.RootElement.GetProperty("result").GetProperty("data").GetProperty(data.Platform ? "events" : "people");
        return rows.EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == data.RowId)
            .GetProperty(data.Platform ? "actorDisplayName" : "displayName").GetString();
    }

    private static string? TextOf(string body)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("result").GetProperty("text").GetString();
    }

    private sealed record RaceData(bool Platform, Guid OwnerId, Guid TargetId, Guid? ScopeTenant, Guid TargetPersonalTenant,
        Guid RowId, string TargetName, string Route, Guid OriginalKey, Guid TargetOwnKey, string OriginalBody, DateTime NowUtc);
    private sealed record Binding(Guid Id, Guid Key, string Route, string? SnapshotJson, string? QuickKey, IReadOnlyList<Guid> Subjects);

    private sealed class RacePhases
    {
        internal bool CompositionArmed;
        internal bool FenceArmed;
        internal int PausedCompositions;
        internal int TotalCompositions;
        internal AssistantQuickCompositionResponse? Prepared;
        internal Guid? PreparedOwner;
        internal Guid? PreparedTenant;
        internal IReadOnlyCollection<Guid> FencedIds = [];
        internal IReadOnlyList<UserDeletionStateRow>? FencedStates;
        internal TaskCompletionSource Composed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ResumeComposition { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Fenced { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ResumeFence { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record PausedQuick(IAssistantQuickService Inner, ApplicationDbContext Database, ICurrentUser User,
        ITenantContext Tenant, RacePhases Phases) : IAssistantQuickService
    {
        public Task<Result<IReadOnlyList<AssistantQuickQueryResponse>>> ListAsync(CancellationToken ct) => Inner.ListAsync(ct);
        public async Task<Result<AssistantQuickCompositionResponse>> ExecuteAsync(string key, CancellationToken ct)
        {
            Assert.Null(Database.Database.CurrentTransaction);
            Interlocked.Increment(ref Phases.TotalCompositions);
            var composition = await Inner.ExecuteAsync(key, ct);
            Assert.Null(Database.Database.CurrentTransaction);
            if (Phases.CompositionArmed && Interlocked.Increment(ref Phases.PausedCompositions) == 1)
            {
                Assert.True(composition.IsSuccess, composition.Error.Code);
                Phases.Prepared = composition.Value;
                Phases.PreparedOwner = User.UserId;
                Phases.PreparedTenant = Tenant.TenantId;
                Phases.Composed.TrySetResult();
                await Phases.ResumeComposition.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
            return composition;
        }
    }

    private sealed record PausedIdentityReader(IUserStatusReader Inner, ApplicationDbContext Database, RacePhases Phases) : IUserStatusReader
    {
        public Task<UserStatus?> GetStatusAsync(Guid id, CancellationToken ct) => Inner.GetStatusAsync(id, ct);
        public Task<IReadOnlyCollection<Guid>> ListActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Inner.ListActiveAsync(ids, ct);
        public Task<Result<IReadOnlyList<UserDeletionStateRow>>> ListDeletionStatesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Inner.ListDeletionStatesAsync(ids, ct);
        public async Task<Result<IReadOnlyList<UserDeletionStateRow>>> LockDeletionStatesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            var states = await Inner.LockDeletionStatesAsync(ids, ct);
            if (Phases.FenceArmed && Phases.Composed.Task.IsCompleted && !Phases.Fenced.Task.IsCompleted)
            {
                Assert.NotNull(Database.Database.CurrentTransaction);
                Assert.True(states.IsSuccess, states.Error.Code);
                Phases.FencedIds = ids;
                Phases.FencedStates = states.Value;
                Phases.Fenced.TrySetResult();
                await Phases.ResumeFence.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
            return states;
        }
    }

    private sealed class ReplayReadPause
    {
        internal bool Armed;
        internal Guid OwnerId;
        internal Guid OperationId;
        internal int ObservedCalls;
        internal Access ObservedAccess;
        internal Guid? ObservedTenant;
        internal AssistantDurableOperationRow? InitialRow;
        internal TaskCompletionSource SecondRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record PausedDurableRepository(IAssistantDurableRepository Inner, ApplicationDbContext Database,
        ITenantContext Tenant, ReplayReadPause Pause) : IAssistantDurableRepository
    {
        // The decorator is scoped: this counter belongs only to this request's repository instance.
        private int _getByIdCalls;
        public Task LockAsync(Guid owner, Access access, Guid key, Guid? conversationId, CancellationToken ct) =>
            Inner.LockAsync(owner, access, key, conversationId, ct);
        public Task<AssistantDurableOperationRow?> GetByKeyAsync(Guid owner, Access access, Guid key, CancellationToken ct) =>
            Inner.GetByKeyAsync(owner, access, key, ct);
        public Entity Add(Guid owner, Access access, IdempotentRequest request, Guid conversationId,
            IdempotentReference reference, string snapshot, string? quickKey, DateTime nowUtc, Guid[]? subjectUserIds = null) =>
            Inner.Add(owner, access, request, conversationId, reference, snapshot, quickKey, nowUtc, subjectUserIds);
        public Task RemoveContentAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct) =>
            Inner.RemoveContentAsync(owner, access, conversationId, ct);
        public Task StageFileCleanupAsync(Guid owner, Access access, Guid conversationId, DateTime nowUtc, CancellationToken ct) =>
            Inner.StageFileCleanupAsync(owner, access, conversationId, nowUtc, ct);
        public async Task<AssistantDurableOperationRow?> GetByIdAsync(Guid owner, Access access, Guid id, CancellationToken ct)
        {
            var observed = Pause.Armed && owner == Pause.OwnerId && id == Pause.OperationId;
            var call = observed ? Interlocked.Increment(ref _getByIdCalls) : 0;
            if (observed)
            {
                Assert.Null(Database.Database.CurrentTransaction);
                Pause.ObservedCalls = call;
                Pause.ObservedAccess = access;
                Pause.ObservedTenant = Tenant.TenantId;
                if (call == 2)
                {
                    Pause.SecondRead.TrySetResult();
                    await Pause.Resume.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                    Assert.Null(Database.Database.CurrentTransaction);
                }
            }
            var row = await Inner.GetByIdAsync(owner, access, id, ct);
            if (call == 1) Pause.InitialRow = row;
            return row;
        }
    }
}
