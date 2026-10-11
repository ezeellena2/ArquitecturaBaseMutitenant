using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantSnapshotPrivacyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Writer_fences_owner_and_subjects_once_in_stable_order_and_propagates_reader_failure()
    {
        var owner = Guid.CreateVersion7();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var reader = new IdentityStub { States = [new(owner, false), new(first, false), new(second, true)] };
        var guard = new AssistantSnapshotPrivacyGuard(reader);
        var result = await guard.LockAsync(owner, [second, first, second, owner], Ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { owner, first, second }.Order(), reader.Locked);
        Assert.Equal(1, reader.LockCalls);
        Assert.Equal(0, reader.ReadCalls);
        reader.Failure = Error.Failure("General.Unexpected", "Synthetic failure.");
        var failed = await guard.LockAsync(owner, [first], Ct);
        Assert.Same(reader.Failure, failed.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Started_or_missing_owner_is_hidden_without_returning_a_composition(bool exists)
    {
        var owner = Guid.CreateVersion7();
        var reader = new IdentityStub { States = exists ? [new(owner, true)] : [] };
        var result = await new AssistantSnapshotPrivacyGuard(reader).LockAsync(owner, [], Ct);
        Assert.Equal(AssistantErrors.ConversationNotFound, result.Error);
    }

    [Fact]
    public async Task Replay_rejects_any_started_or_missing_subject_and_never_acquires_a_write_lock()
    {
        var owner = Guid.CreateVersion7();
        var subject = Guid.CreateVersion7();
        var reader = new IdentityStub { States = [new(owner, false), new(subject, true)] };
        var guard = new AssistantSnapshotPrivacyGuard(reader);
        Assert.Equal(AssistantErrors.ConversationNotFound, (await guard.CheckAsync(owner, [subject], Ct)).Error);
        reader.States = [new(owner, false)];
        Assert.Equal(AssistantErrors.ConversationNotFound, (await guard.CheckAsync(owner, [subject], Ct)).Error);
        reader.States = [new(owner, false), new(subject, false)];
        Assert.True((await guard.CheckAsync(owner, [subject], Ct)).IsSuccess);
        Assert.Equal(3, reader.ReadCalls);
        Assert.Equal(0, reader.LockCalls);
    }

    [Theory]
    [InlineData(AssistantSnapshotCopyKind.PeopleDisplayName)]
    [InlineData(AssistantSnapshotCopyKind.InvitationDisplayName)]
    [InlineData(AssistantSnapshotCopyKind.RecoveryDisplayName)]
    [InlineData(AssistantSnapshotCopyKind.AuditActorDisplayName)]
    [InlineData(AssistantSnapshotCopyKind.SecurityActorDisplayName)]
    public void Protection_removes_only_the_exact_typed_personal_copy_and_keeps_other_fields(AssistantSnapshotCopyKind kind)
    {
        var started = Guid.CreateVersion7();
        var live = Guid.CreateVersion7();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var copies = new[] { new AssistantSnapshotSubjectCopy(started, kind, first), new AssistantSnapshotSubjectCopy(live, kind, second) };
        var composition = Composition(Data(kind, first, second), copies);
        var protectedResult = AssistantSnapshotPrivacyGuard.Protect(composition, [new(started, true), new(live, false)]);
        Assert.True(protectedResult.IsSuccess);
        var expected = Composition(Data(kind, first, second, null), [copies[1]]);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(protectedResult.Value));
        Assert.Equal(new[] { copies[1] }, protectedResult.Value.SubjectCopies);
        Assert.Equal(new[] { copies[1] }, protectedResult.Value.Result.SubjectCopies);
        Assert.Equal("Personal copy", Name(composition.Result.Data));
    }

    [Fact]
    public void Living_subjects_return_the_exact_prepared_composition_and_missing_subjects_are_protected()
    {
        var subject = Guid.CreateVersion7();
        var row = Guid.CreateVersion7();
        var composition = Composition(new AssistantPeopleQuickDataResponse([new(row, "Personal copy", "Active")], 1, false),
            [new(subject, AssistantSnapshotCopyKind.PeopleDisplayName, row)]);
        Assert.Same(composition, AssistantSnapshotPrivacyGuard.Protect(composition, [new(subject, false)]).Value);
        var missing = AssistantSnapshotPrivacyGuard.Protect(composition, []).Value;
        Assert.Null(Name(missing.Result.Data));
        Assert.Empty(missing.SubjectCopies);
    }

    [Fact]
    public void Subject_sidecar_and_real_audit_actor_are_excluded_from_wire_and_storage_json()
    {
        var subject = Guid.CreateVersion7();
        var row = Guid.CreateVersion7();
        var copies = new[] { new AssistantSnapshotSubjectCopy(subject, AssistantSnapshotCopyKind.PeopleDisplayName, row) };
        var composition = Composition(new AssistantPeopleQuickDataResponse([new(row, "Personal copy", "Active")], 1, false), copies);
        var tool = new AssistantQuickToolData(composition.Result.Data) { SubjectCopies = copies };
        var conversation = new AssistantConversationSummaryResponse(Guid.CreateVersion7(), "Title", false, 1, NowUtc, NowUtc);
        var storage = AssistantSnapshotBuilder.Serialize(new AssistantDurableSnapshot(conversation, [], composition.Result));
        foreach (var json in new[] { JsonSerializer.Serialize(tool), JsonSerializer.Serialize(composition), JsonSerializer.Serialize(composition.Result), storage })
        {
            Assert.DoesNotContain(subject.ToString(), json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SubjectCopies", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CopyKind", json, StringComparison.OrdinalIgnoreCase);
        }
        var target = Guid.CreateVersion7();
        var audit = new PlatformAuditEventResponse(row, NowUtc, "Accounts", "Suspend", "Summary", "Target", null, target,
            "Actor", "Operator", null, null) { AuditActorId = subject };
        Assert.NotEqual(audit.TargetAccountId, audit.AuditActorId);
        Assert.DoesNotContain(subject.ToString(), JsonSerializer.Serialize(audit), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(target.ToString(), JsonSerializer.Serialize(audit), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(AssistantSnapshotBuilder.Restore(storage, 1).Result!.SubjectCopies);
    }

    private static AssistantQuickCompositionResponse Composition(AssistantQuickDataResponse data,
        IReadOnlyList<AssistantSnapshotSubjectCopy> copies) => new("Title", new("Summary stays", [], data) { SubjectCopies = copies })
        { SubjectCopies = copies };

    private static AssistantQuickDataResponse Data(AssistantSnapshotCopyKind kind, Guid first, Guid second, string? firstName = "Personal copy") => kind switch
    {
        AssistantSnapshotCopyKind.PeopleDisplayName => new AssistantPeopleQuickDataResponse([new(first, firstName, "Active"), new(second, "Living copy", "Active")], 37, true),
        AssistantSnapshotCopyKind.InvitationDisplayName => new AssistantInvitationsQuickDataResponse([new(first, firstName, "Expired", NowUtc), new(second, "Living copy", "Pending", NowUtc)], 37, true),
        AssistantSnapshotCopyKind.RecoveryDisplayName => new AssistantRecoveriesQuickDataResponse([new(first, firstName, "Pending", NowUtc), new(second, "Living copy", "Pending", NowUtc)], 37, true),
        AssistantSnapshotCopyKind.AuditActorDisplayName => new AssistantAuditQuickDataResponse([new(first, "Updated", firstName, NowUtc), new(second, "Created", "Living copy", NowUtc)], "cursor", true),
        AssistantSnapshotCopyKind.SecurityActorDisplayName => new AssistantSecurityQuickDataResponse([new(first, "Updated", firstName, NowUtc), new(second, "Created", "Living copy", NowUtc)], "cursor", true),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string? Name(AssistantQuickDataResponse data) => data switch
    {
        AssistantPeopleQuickDataResponse value => value.People[0].DisplayName,
        AssistantInvitationsQuickDataResponse value => value.Invitations[0].DisplayName,
        AssistantRecoveriesQuickDataResponse value => value.Recoveries[0].DisplayName,
        AssistantAuditQuickDataResponse value => value.Changes[0].ActorDisplayName,
        AssistantSecurityQuickDataResponse value => value.Events[0].ActorDisplayName,
        _ => throw new ArgumentOutOfRangeException(nameof(data)),
    };

    private sealed class IdentityStub : IUserStatusReader
    {
        internal IReadOnlyList<UserDeletionStateRow> States { get; set; } = [];
        internal Error? Failure { get; set; }
        internal IReadOnlyCollection<Guid> Locked { get; private set; } = [];
        internal int ReadCalls { get; private set; }
        internal int LockCalls { get; private set; }
        public Task<UserStatus?> GetStatusAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> ListActiveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<UserDeletionStateRow>>> ListDeletionStatesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        {
            ReadCalls++;
            return Task.FromResult(Failure is { } error ? Result.Failure<IReadOnlyList<UserDeletionStateRow>>(error) : Result.Success(States));
        }
        public Task<Result<IReadOnlyList<UserDeletionStateRow>>> LockDeletionStatesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        {
            LockCalls++;
            Locked = userIds;
            return Task.FromResult(Failure is { } error ? Result.Failure<IReadOnlyList<UserDeletionStateRow>>(error) : Result.Success(States));
        }
    }
}
