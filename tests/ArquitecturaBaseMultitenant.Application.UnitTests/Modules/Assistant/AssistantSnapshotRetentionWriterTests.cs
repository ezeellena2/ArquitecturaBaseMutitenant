using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantSnapshotRetentionWriterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime CreatedAtUtc = new(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Expiry_rechecks_original_creation_at_the_exact_boundary_after_the_lock()
    {
        var row = Operation(Guid.CreateVersion7());
        var repository = new CleanupRepository { Locked = new(true, row) };
        var writer = new AssistantSnapshotRetentionWriter(repository);
        var candidate = new AssistantSnapshotRetentionCandidate(row.Id, null);
        Assert.Equal(0, await writer.PurgeExpiredAsync(candidate, CreatedAtUtc.AddHours(24).AddTicks(-1), Ct));
        Assert.Equal("{}", row.SnapshotJson);
        Assert.Empty(repository.RemovedIndices);
        Assert.Equal(1, await writer.PurgeExpiredAsync(candidate, CreatedAtUtc.AddHours(24), Ct));
        Assert.Null(row.SnapshotJson);
        Assert.Null(row.QuickKey);
        Assert.Empty(row.SubjectUserIds);
        Assert.Equal(new[] { row.Id }, repository.RemovedIndices);
        Assert.Equal("hash", row.BodyHash);
    }

    [Fact]
    public async Task A_busy_operation_keeps_the_index_for_retry_and_a_missing_operation_removes_its_orphan_index()
    {
        var repository = new CleanupRepository { Locked = new(false, null) };
        var writer = new AssistantSnapshotRetentionWriter(repository);
        var candidate = new AssistantSnapshotRetentionCandidate(Guid.CreateVersion7(), null);
        Assert.Equal(0, await writer.PurgeExpiredAsync(candidate, CreatedAtUtc.AddDays(2), Ct));
        Assert.Empty(repository.RemovedIndices);
        repository.Locked = new(true, null);
        Assert.Equal(1, await writer.PurgeExpiredAsync(candidate, CreatedAtUtc.AddDays(2), Ct));
        Assert.Equal(new[] { candidate.OperationId }, repository.RemovedIndices);
    }

    [Fact]
    public async Task Account_deletion_removes_owned_bindings_and_only_the_content_of_other_owners()
    {
        var userId = Guid.CreateVersion7();
        var owned = Operation(userId);
        var other = Operation(Guid.CreateVersion7(), userId);
        var repository = new CleanupRepository { AccountOperations = [owned, other] };
        await new AssistantSnapshotRetentionWriter(repository).PurgeAccountAsync(userId, Ct);
        Assert.Equal(new[] { owned }, repository.RemovedOperations);
        Assert.Null(other.SnapshotJson);
        Assert.Equal("hash", other.BodyHash);
        Assert.Equal(new[] { owned.Id, other.Id }, repository.RemovedIndices);
    }

    private static PlatformAssistantDurableOperation Operation(Guid owner, Guid? subject = null) =>
        PlatformAssistantDurableOperation.Create(owner, Guid.CreateVersion7(), "hash", "/quick", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{}", "fixture", CreatedAtUtc, [subject ?? Guid.CreateVersion7()]);

    private sealed class CleanupRepository : IAssistantSnapshotRetentionRepository
    {
        internal AssistantSnapshotCleanupRow Locked { get; set; } = new(false, null);
        internal IReadOnlyList<AssistantDurableOperationBase> AccountOperations { get; set; } = [];
        internal List<Guid> RemovedIndices { get; } = [];
        internal List<AssistantDurableOperationBase> RemovedOperations { get; } = [];
        public Task<IReadOnlyList<AssistantSnapshotRetentionCandidate>> ListExpiredAsync(DateTime nowUtc, CancellationToken ct,
            AssistantSnapshotRetentionCandidate? after = null) => throw new NotSupportedException();
        public Task<AssistantSnapshotCleanupRow> LockExpiredAsync(AssistantSnapshotRetentionCandidate candidate, DateTime nowUtc, CancellationToken ct) => Task.FromResult(Locked);
        public Task RemoveIndicesAsync(IReadOnlyList<Guid> operationIds, CancellationToken ct) { RemovedIndices.AddRange(operationIds); return Task.CompletedTask; }
        public Task<IReadOnlyList<AssistantDurableOperationBase>> LockAccountOperationsAsync(Guid userId, CancellationToken ct) => Task.FromResult(AccountOperations);
        public void RemoveOperation(AssistantDurableOperationBase operation) => RemovedOperations.Add(operation);
    }
}
