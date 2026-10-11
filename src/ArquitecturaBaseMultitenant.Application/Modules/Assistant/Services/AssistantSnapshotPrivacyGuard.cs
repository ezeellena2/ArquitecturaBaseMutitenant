using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Fences new copies before durable locks and rejects compromised original snapshots on reads.</summary>
internal sealed class AssistantSnapshotPrivacyGuard(IUserStatusReader identities)
{
    internal async Task<Result<IReadOnlyList<UserDeletionStateRow>>> LockAsync(Guid owner,
        IReadOnlyCollection<Guid> subjects, CancellationToken ct)
    {
        var result = await identities.LockDeletionStatesAsync(Accounts(owner, subjects), ct);
        if (result.IsFailure) return result.Error;
        return Started(owner, result.Value) ? AssistantErrors.ConversationNotFound : result;
    }

    // A replay uses the original snapshot verbatim; it is never adapted by Protect.
    internal async Task<Result> CheckAsync(Guid owner, IReadOnlyCollection<Guid> subjects, CancellationToken ct)
    {
        var accounts = Accounts(owner, subjects);
        var result = await identities.ListDeletionStatesAsync(accounts, ct);
        if (result.IsFailure) return result.Error;
        return accounts.Any(id => Started(id, result.Value)) ? AssistantErrors.ConversationNotFound : Result.Success();
    }

    internal static Result<AssistantQuickCompositionResponse> Protect(AssistantQuickCompositionResponse composition,
        IReadOnlyList<UserDeletionStateRow> states)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(states);
        var removed = composition.SubjectCopies.Where(copy => Started(copy.AccountId, states)).ToArray();
        if (removed.Length == 0) return composition;
        var copies = composition.SubjectCopies.Except(removed).ToArray();
        bool Remove(AssistantSnapshotCopyKind kind, Guid rowId) => removed.Any(copy => copy.CopyKind == kind && copy.RowId == rowId);
        AssistantQuickDataResponse data = composition.Result.Data switch
        {
            AssistantPeopleQuickDataResponse value => value with
            {
                People = value.People.Select(row => Remove(AssistantSnapshotCopyKind.PeopleDisplayName, row.Id)
                    ? row with { DisplayName = null } : row).ToArray(),
            },
            AssistantInvitationsQuickDataResponse value => value with
            {
                Invitations = value.Invitations.Select(row => Remove(AssistantSnapshotCopyKind.InvitationDisplayName, row.Id)
                    ? row with { DisplayName = null } : row).ToArray(),
            },
            AssistantRecoveriesQuickDataResponse value => value with
            {
                Recoveries = value.Recoveries.Select(row => Remove(AssistantSnapshotCopyKind.RecoveryDisplayName, row.Id)
                    ? row with { DisplayName = null } : row).ToArray(),
            },
            AssistantAuditQuickDataResponse value => value with
            {
                Changes = value.Changes.Select(row => Remove(AssistantSnapshotCopyKind.AuditActorDisplayName, row.Id)
                    ? row with { ActorDisplayName = null } : row).ToArray(),
            },
            AssistantSecurityQuickDataResponse value => value with
            {
                Events = value.Events.Select(row => Remove(AssistantSnapshotCopyKind.SecurityActorDisplayName, row.Id)
                    ? row with { ActorDisplayName = null } : row).ToArray(),
            },
            _ => composition.Result.Data,
        };
        return composition with { Result = composition.Result with { Data = data, SubjectCopies = copies }, SubjectCopies = copies };
    }

    private static Guid[] Accounts(Guid owner, IReadOnlyCollection<Guid> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        return subjects.Append(owner).Distinct().Order().ToArray();
    }

    private static bool Started(Guid id, IReadOnlyList<UserDeletionStateRow> states) =>
        states.FirstOrDefault(state => state.UserId == id)?.HasDeletionStarted ?? true;
}
