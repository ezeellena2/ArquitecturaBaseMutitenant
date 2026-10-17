using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantTurnBuilder(IAssistantTurnRepository turns, IAssistantDurableRepository durable,
    AssistantConversationPolicy policy, ICurrentUser user, AssistantSnapshotPrivacyGuard privacy, TimeProvider clock)
{
    private Guid Owner => user.UserId ?? throw new InvalidOperationException("A session is required.");
    private Domain.Users.Access Access => user.Access ?? throw new InvalidOperationException("An access is required.");
    internal async Task<Result<AssistantTurnRead>> ReadAsync(Guid id, int after, CancellationToken ct)
    {
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        var turn = await turns.GetAsync(Owner, Access, id, false, ct);
        if (turn is null) return AssistantErrors.TurnNotFound;
        var binding = await durable.GetByIdAsync(Owner, Access, turn.BindingId, ct);
        if (binding?.SnapshotJson is null) return AssistantErrors.ConversationNotFound;
        var frames = new List<AssistantTurnFrame>();
        if (after < 0) frames.Add(AssistantTurnJson.Frame("start", 0,
            new AssistantStartEventResponse(id, turn.UserMessageId, turn.AssistantMessageId, turn.AdmissionVersion ?? binding.Version)));
        frames.AddRange(AssistantTurnJson.Restore<List<AssistantTurnFrame>>(turn.EventsJson).Where(frame => frame.Sequence > after));
        if (frames.Any(frame => frame.Event == "title"))
        {
            var conversation = await turns.GetConversationAsync(Owner, Access, turn.ConversationId, null, ct);
            if (conversation is null) return AssistantErrors.ConversationNotFound;
            if (conversation.IsTitleManual) frames.RemoveAll(frame => frame.Event == "title");
            for (var index = 0; index < frames.Count; index++)
                if (frames[index].Event == "title")
                {
                    var title = frames[index].Data.Deserialize<AssistantTitleEventResponse>(AssistantTurnJson.Options)!;
                    frames[index] = AssistantTurnJson.Frame("title", frames[index].Sequence, title with { Version = conversation.Version });
                }
        }
        var terminal = turn.Status is AssistantTurnStatus.Complete or AssistantTurnStatus.Stopped or AssistantTurnStatus.Failed;
        if (terminal && turn.Sequence + 1 > after)
        {
            var snapshot = Restore(binding);
            frames.Add(snapshot.ErrorCode is { } code
                ? AssistantTurnJson.Frame("error", turn.Sequence + 1, new AssistantErrorEventResponse(id, code, id.ToString("N"), null, AssistantFailedStatus.Failed))
                : AssistantTurnJson.Frame("done", turn.Sequence + 1, snapshot.Result));
        }
        // Authorization and privacy are checked again after reading content, before returning a frame batch.
        allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        var safe = await privacy.CheckAsync(Owner, binding.SubjectUserIds, ct);
        if (safe.IsFailure) return safe.Error;
        if (await turns.GetAsync(Owner, Access, id, false, ct) is null) return AssistantErrors.TurnNotFound;
        return new AssistantTurnRead(frames, terminal);
    }
    internal async Task<Result<IdempotentReplayResponse>> ReplayAsync(IdempotentReference reference, CancellationToken ct)
    {
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        var binding = await durable.GetByIdAsync(Owner, Access, reference.Id, ct);
        if (binding is null || binding.Reference != reference || !AssistantSnapshotRetentionPolicy.HasContent(binding, clock.GetUtcNow().UtcDateTime)) return AssistantErrors.ConversationNotFound;
        Guid turnId;
        try { turnId = AssistantTurnJson.Restore<Guid>(binding.SnapshotJson!); }
        catch (System.Text.Json.JsonException) { turnId = Restore(binding).Result.TurnId; }
        var turn = await turns.GetAsync(Owner, Access, turnId, false, ct);
        if (turn is null) return AssistantErrors.ConversationNotFound;
        if (turn.Status is AssistantTurnStatus.Pending or AssistantTurnStatus.Running) return AssistantErrors.TurnInProgress;
        allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        var safe = await privacy.CheckAsync(Owner, binding.SubjectUserIds, ct);
        if (safe.IsFailure) return safe.Error;
        binding = await durable.GetByIdAsync(Owner, Access, reference.Id, ct);
        if (binding is null || !AssistantSnapshotRetentionPolicy.HasContent(binding, clock.GetUtcNow().UtcDateTime) ||
            await turns.GetAsync(Owner, Access, turnId, false, ct) is null) return AssistantErrors.ConversationNotFound;
        return new IdempotentReplayResponse<AssistantTurnResultResponse>(reference, Restore(binding).Result);
    }
    internal async Task<Result<bool>> CheckAsync(Guid id, Guid lease, CancellationToken ct)
    {
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        var turn = await turns.GetAsync(Owner, Access, id, false, ct);
        if (turn is null) return AssistantErrors.TurnNotFound;
        var bound = await durable.GetByIdAsync(Owner, Access, turn.BindingId, ct);
        if (bound?.SnapshotJson is null) return AssistantErrors.ConversationNotFound;
        var protectedSubjects = await privacy.CheckAsync(Owner, bound.SubjectUserIds, ct);
        if (protectedSubjects.IsFailure) return protectedSubjects.Error;
        var work = await turns.GetWorkAsync(id, false, ct);
        return work is not null && !work.IsTerminal && work.LeaseId == lease && !work.StopRequested && work.DeadlineAtUtc > clock.GetUtcNow().UtcDateTime;
    }
    private static AssistantTurnSnapshot Restore(AssistantDurableOperationRow binding)
    {
        var snapshot = AssistantTurnJson.Restore<AssistantTurnSnapshot>(binding.SnapshotJson!);
        return snapshot with { Result = snapshot.Result with { ConversationVersion = binding.Version, Message = snapshot.Result.Message with { Version = binding.Version } } };
    }
}
