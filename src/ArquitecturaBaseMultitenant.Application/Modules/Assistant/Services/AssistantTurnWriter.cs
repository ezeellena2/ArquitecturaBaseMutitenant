using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantTurnWriter(IAssistantTurnRepository turns, IAssistantDurableRepository durable,
    AssistantConversationPolicy policy, AssistantUsageWriter usage, IAssistantFinanceRepository finance,
    ICurrentUser user, ITenantContext tenant, AssistantSnapshotPrivacyGuard privacy, TimeProvider clock)
{
    private Guid Owner => user.UserId ?? throw new InvalidOperationException("An execution actor is required.");
    private Domain.Users.Access Access => user.Access ?? throw new InvalidOperationException("An execution access is required.");
    private AssistantFinancialBinding Binding => new(Owner, Access, tenant.TenantId);

    internal async Task<Result<AssistantTurnExecution>> ClaimAsync(Guid id, Guid lease, CancellationToken ct)
    {
        var loaded = await LoadAsync(id, null, true, ct);
        if (loaded.IsFailure) return loaded.Error;
        var (turn, work) = loaded.Value;
        var recover = work.CallStarted || work.CallSequence != 0 || work.StopRequested || work.DeadlineAtUtc <= clock.GetUtcNow().UtcDateTime;
        if (!work.Claim(lease, clock.GetUtcNow().UtcDateTime)) return AssistantErrors.TurnInProgress;
        var admitted = await durable.GetByIdAsync(Owner, Access, turn.BindingId, ct)
            ?? throw new InvalidOperationException("The admitted turn binding is missing.");
        turn.CaptureAdmissionVersion(admitted.Version);
        turn.Finish("Running");
        IReadOnlyList<AssistantMessageResponse> messages = [];
        if (!recover)
        {
            var metadata = (await turns.FindHistoryPrivacyAsync(Owner, Access, turn.ConversationId, ct))
                .Where(message => turn.ContextMessageIds.Contains(message.Id)).ToArray();
            if (metadata.Length != turn.ContextMessageIds.Length || !AssistantTurnPreparationBuilder.HasKnownProvenance(metadata))
                return AssistantErrors.ConversationNotFound;
            var contextSubjects = metadata.SelectMany(message => message.SubjectUserIds).Distinct().ToArray();
            if (contextSubjects.Except(admitted.SubjectUserIds).Any()) return AssistantErrors.ConversationNotFound;
            var safe = await privacy.CheckAsync(Owner, contextSubjects, ct);
            if (safe.IsFailure) return safe.Error;
            messages = await turns.ListMessagesAsync(Owner, Access, turn.ConversationId, ct, turn.ContextMessageIds);
            if (messages.Count != turn.ContextMessageIds.Length) return AssistantErrors.ConversationNotFound;
            safe = await privacy.CheckAsync(Owner, contextSubjects, ct);
            if (safe.IsFailure) return safe.Error;
        }
        var prompt = new List<Microsoft.Extensions.AI.ChatMessage>();
        if (!recover)
        {
            var system = Resources.AssistantTexts.ResourceManager.GetString("Runtime.SystemPrompt", System.Globalization.CultureInfo.GetCultureInfo(work.Culture))
                ?? throw new InvalidOperationException("The assistant system resource is missing.");
            prompt.Add(new(Microsoft.Extensions.AI.ChatRole.System, system));
            foreach (var message in messages.Where(row => row.Id != turn.AssistantMessageId))
                prompt.Add(new(message.Role == AssistantMessageRole.User ? Microsoft.Extensions.AI.ChatRole.User : Microsoft.Extensions.AI.ChatRole.Assistant, message.Text));
        }
        return new AssistantTurnExecution(id, lease, prompt, [], new AssistantChatBinding(
            work.Funding == AssistantKeyFunding.OwnKey ? AssistantMode.OwnKey : AssistantMode.PlatformKey,
            work.ModelId, work.ProviderModelId, Enum.Parse<AssistantEffort>(work.Effort)), recover, turn.ConversationId,
            messages.Where(row => row.Id != turn.AssistantMessageId).ToArray());
    }
    internal async Task<Result> BeginCallAsync(Guid id, Guid lease, int sequence, CancellationToken ct)
    {
        var loaded = await LoadAsync(id, lease, true, ct);
        if (loaded.IsFailure) return loaded.Error;
        var (_, work) = loaded.Value;
        if (work.StopRequested || work.DeadlineAtUtc <= clock.GetUtcNow().UtcDateTime || work.CallStarted) return AssistantErrors.TurnInProgress;
        var call = await usage.BeginCallAsync(Binding, id, sequence, false, ct);
        if (call.IsFailure) return call.Error;
        if (!call.Value.IsNew) return AssistantErrors.TurnInProgress;
        work.BeginCall(sequence); work.Renew(clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
    internal async Task<Result<AssistantTitleCall?>> BeginTitleAsync(Guid id, Guid lease, CancellationToken ct)
    {
        var loaded = await LoadAsync(id, lease, true, ct);
        if (loaded.IsFailure) return loaded.Error;
        var (turn, work) = loaded.Value;
        if (work.StopRequested || work.CallStarted || work.CallSequence >= AssistantTechnicalLimits.MaxCallsPerTurn)
            return Result.Success<AssistantTitleCall?>(null);
        var conversation = await turns.GetConversationAsync(Owner, Access, turn.ConversationId, null, ct);
        var operation = await finance.GetOperationAsync(Binding, id, ct);
        if (conversation is null || conversation.Title is not null || conversation.IsTitleManual || operation?.TitlePrice is not { } price)
            return Result.Success<AssistantTitleCall?>(null);
        var call = await usage.BeginCallAsync(Binding, id, work.CallSequence + 1, true, ct);
        if (call.IsFailure) return call.Error;
        if (!call.Value.IsNew) return Result.Success<AssistantTitleCall?>(null);
        work.BeginCall(work.CallSequence + 1); work.Renew(clock.GetUtcNow().UtcDateTime);
        var rows = await turns.ListMessagesAsync(Owner, Access, turn.ConversationId, ct, [turn.UserMessageId, turn.AssistantMessageId]);
        var system = Resources.AssistantTexts.ResourceManager.GetString("Runtime.TitlePrompt", System.Globalization.CultureInfo.GetCultureInfo(work.Culture))!;
        return new AssistantTitleCall(new AssistantChatBinding(work.Funding == AssistantKeyFunding.OwnKey ? AssistantMode.OwnKey : AssistantMode.PlatformKey,
            price.ModelId, price.ProviderModelId, AssistantEffort.Low), conversation.Version, work.CallSequence,
            [new(Microsoft.Extensions.AI.ChatRole.System, system), new(Microsoft.Extensions.AI.ChatRole.User, string.Join("\n", rows.Select(row => row.Text)))]);
    }
    internal async Task<Result> FinishTitleAsync(Guid id, Guid lease, uint version, string? title, AssistantCallUsage? counts, CancellationToken ct)
    {
        var loaded = await LoadAsync(id, lease, true, ct);
        if (loaded.IsFailure) return loaded.Error;
        var (turn, work) = loaded.Value;
        if (counts is not null)
        {
            var recorded = await usage.RecordUsageAsync(Binding, id, work.CallSequence, counts, ct);
            if (recorded.IsFailure) return recorded;
        }
        work.EndCall(); work.Renew(clock.GetUtcNow().UtcDateTime);
        var conversation = await turns.GetConversationAsync(Owner, Access, turn.ConversationId, null, ct);
        var normalized = title?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > Domain.Common.TextLimits.AssistantTitle)
        {
            var rows = await turns.ListMessagesAsync(Owner, Access, turn.ConversationId, ct, [turn.UserMessageId]);
            normalized = rows.SingleOrDefault()?.Text?.Trim();
            if (normalized?.Length > Domain.Common.TextLimits.AssistantTitle) normalized = normalized[..Domain.Common.TextLimits.AssistantTitle];
        }
        if (conversation is not null && !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= Domain.Common.TextLimits.AssistantTitle
            && conversation.TrySetAutomaticTitle(normalized, version, clock.GetUtcNow().UtcDateTime))
            Append(turn, "title", new AssistantTitleEventResponse(turn.ConversationId, normalized, 0));
        return Result.Success();
    }
    internal async Task<Result> HeartbeatAsync(Guid id, Guid lease, CancellationToken ct)
    {
        var loaded = await LoadAsync(id, lease, true, ct);
        if (loaded.IsFailure) return loaded.Error;
        var (_, work) = loaded.Value;
        if (work.StopRequested || work.DeadlineAtUtc <= clock.GetUtcNow().UtcDateTime) return AssistantErrors.TurnInProgress;
        work.Renew(clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
    internal async Task<Result> CheckpointAsync(Guid id, Guid lease, string? delta, AssistantCallUsage? counts, bool ended, CancellationToken ct)
    {
        var loaded = await LoadAsync(id, lease, true, ct);
        if (loaded.IsFailure) return loaded.Error;
        var (turn, work) = loaded.Value;
        if (work.StopRequested) return AssistantErrors.TurnInProgress;
        if (counts is not null)
        {
            var recorded = await usage.RecordUsageAsync(Binding, id, work.CallSequence, counts, ct);
            if (recorded.IsFailure) return recorded;
        }
        if (!string.IsNullOrEmpty(delta))
        {
            await turns.CheckpointMessageAsync(Owner, Access, turn, delta, null, ct);
            Append(turn, "delta", new AssistantDeltaEventResponse(id, turn.Sequence + 1, delta));
        }
        if (ended) work.EndCall();
        work.Renew(clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
    internal async Task<Result> ToolStateAsync(Guid id, Guid lease, string key, string label, AssistantToolState state, CancellationToken ct)
    {
        var loaded = await LoadAsync(id, lease, true, ct);
        if (loaded.IsFailure) return loaded.Error;
        var (turn, work) = loaded.Value;
        if (work.StopRequested) return AssistantErrors.TurnInProgress;
        Append(turn, "tool", new AssistantToolEventResponse(id, key, state, label));
        work.Renew(clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
    internal async Task<Result> BindSubjectsAsync(Guid id, Guid lease, IReadOnlyList<Guid> subjects, CancellationToken ct)
    {
        var loaded = await LoadAsync(id, lease, true, ct, subjects);
        if (loaded.IsFailure) return loaded.Error;
        var (turn, work) = loaded.Value;
        if (work.StopRequested) return AssistantErrors.TurnInProgress;
        await turns.AddSubjectsAsync(Owner, Access, turn.BindingId, subjects, ct);
        return Result.Success();
    }
    internal async Task<Result> CompleteAsync(Guid id, Guid lease, string status, string? code, CancellationToken ct)
    {
        // Final accounting does not depend on live permission/feature/notice. It uses the admitted binding.
        var hint = await turns.GetAsync(Owner, Access, id, false, ct);
        var mayWriteContent = false;
        if (hint is not null)
        {
            var original = await durable.GetByIdAsync(Owner, Access, hint.BindingId, ct);
            var fenced = await privacy.LockAsync(Owner, original?.SubjectUserIds ?? [], ct);
            mayWriteContent = fenced.IsSuccess;
            await durable.LockAsync(Owner, Access, Guid.Empty, hint.ConversationId, ct);
        }
        var work = await turns.GetWorkAsync(id, true, ct);
        if (work is null || work.OwnerUserId != Owner || work.Access != Access || work.ScopeTenantId != tenant.TenantId) return AssistantErrors.TurnNotFound;
        if (work.IsTerminal) return Result.Success();
        if (work.LeaseId != lease) return AssistantErrors.TurnInProgress;
        if (work.StopRequested) status = "Stopped";
        else if (work.DeadlineAtUtc <= clock.GetUtcNow().UtcDateTime) { status = "Failed"; code = AssistantErrors.ProviderUnavailable.Code; }
        var completed = await usage.CompleteAsync(Binding, id, ct);
        if (completed.IsFailure) return completed;
        work.Finish(status);
        // Deletion can race a provider callback; no deleted content is reconstructed.
        if (!mayWriteContent) return Result.Success();
        var safe = await privacy.CheckAsync(Owner, [], ct);
        if (safe.IsFailure) return Result.Success();
        var turn = await turns.GetAsync(Owner, Access, id, true, ct);
        if (turn is null) return Result.Success();
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure)
        {
            // Only technical terminal metadata changes after revocation; do not read partial chat for a new snapshot.
            turn.Finish("Failed"); work.Finish("Failed");
            await turns.FinishMessageStatusAsync(Owner, Access, turn, "Failed", ct);
            await turns.SetSnapshotAsync(Owner, Access, turn.BindingId, null, ct);
            return Result.Success();
        }
        var binding = await durable.GetByIdAsync(Owner, Access, turn.BindingId, ct);
        if (binding?.SnapshotJson is null) return Result.Success();
        var safeSubjects = await privacy.CheckAsync(Owner, binding.SubjectUserIds, ct);
        if (safeSubjects.IsFailure)
        {
            turn.Finish("Failed"); work.Finish("Failed");
            await turns.FinishMessageStatusAsync(Owner, Access, turn, "Failed", ct);
            await turns.SetSnapshotAsync(Owner, Access, turn.BindingId, null, ct);
            return Result.Success();
        }
        await turns.CheckpointMessageAsync(Owner, Access, turn, null, status, ct);
        turn.Finish(status);
        var messages = await turns.ListMessagesAsync(Owner, Access, turn.ConversationId, ct, [turn.AssistantMessageId]);
        var message = messages.Single(row => row.Id == turn.AssistantMessageId) with { Status = Enum.Parse<AssistantMessageStatus>(status), Version = 0 };
        var operation = await finance.GetOperationAsync(Binding, id, ct) ?? throw new InvalidOperationException("The admitted operation is missing.");
        var calls = await finance.ListCallsAsync(id, ct);
        var cost = new Money(calls.Sum(row => row.Cost.Amount), operation.ReservedCost.Currency).Round(12);
        var consumed = operation.Funding == AssistantKeyFunding.PlatformKey
            ? (await finance.GetPlatformQuotaAsync(Owner, operation.DayUtc, ct))!.ConsumedQueries
            : (await finance.GetOwnQuotaAsync(Owner, operation.DayUtc, ct))!.ConsumedQueries;
        var reserved = operation.Funding == AssistantKeyFunding.PlatformKey
            ? (await finance.GetPlatformQuotaAsync(Owner, operation.DayUtc, ct))!.ReservedQueries
            : (await finance.GetOwnQuotaAsync(Owner, operation.DayUtc, ct))!.ReservedQueries;
        var remaining = Math.Max(0, operation.DailyLimit - consumed - reserved);
        Money? monthlyUsed = operation.BudgetMonthId is { } monthId
            ? new Money(await finance.GetCommittedCostAsync(monthId, operation.Funding, ct), cost.Currency).Round(12) : null;
        var result = new AssistantTurnResultResponse(id, message, 0, new AssistantTurnUsageResponse(
            checked((int)calls.Sum(row => row.InputTokens)), checked((int)calls.Sum(row => row.OutputTokens)),
            checked((int)calls.Sum(row => row.CacheWrite5MinutesTokens + row.CacheWrite1HourTokens + row.CacheWriteUnknownTokens)),
            checked((int)calls.Sum(row => row.CacheReadTokens)), cost, remaining <= 10 ? remaining : null, monthlyUsed));
        var conversation = await turns.GetConversationAsync(Owner, Access, turn.ConversationId, null, ct);
        if (conversation is null) return Result.Success();
        turns.Touch(conversation, clock.GetUtcNow().UtcDateTime);
        await turns.SetSnapshotAsync(Owner, Access, turn.BindingId, AssistantTurnJson.Serialize(new AssistantTurnSnapshot(result, status == "Failed" ? code ?? AssistantErrors.ProviderUnavailable.Code : null)), ct);
        return Result.Success();
    }
    internal async Task<Result> RecoverAsync(Guid id, Guid lease, CancellationToken ct)
    {
        var work = await turns.GetWorkAsync(id, true, ct);
        if (work is null || work.ScopeTenantId != tenant.TenantId) return AssistantErrors.TurnNotFound;
        if (work.IsTerminal) return Result.Success();
        if (work.LeaseId != lease && work.LeaseExpiresAtUtc > clock.GetUtcNow().UtcDateTime) return AssistantErrors.TurnInProgress;
        var binding = new AssistantFinancialBinding(work.OwnerUserId, work.Access, work.ScopeTenantId);
        var completed = await usage.CompleteAsync(binding, id, ct);
        if (completed.IsFailure) return completed;
        work.Finish("Failed");
        var turn = await turns.GetAsync(work.OwnerUserId, work.Access, id, true, ct);
        if (turn is not null)
        {
            turn.Finish("Failed");
            await turns.FinishMessageStatusAsync(work.OwnerUserId, work.Access, turn, "Failed", ct);
            await turns.SetSnapshotAsync(work.OwnerUserId, work.Access, turn.BindingId, null, ct);
        }
        return Result.Success();
    }
    private async Task<Result<(IAssistantTurn Turn, AssistantTurnWork Work)>> LoadAsync(Guid id, Guid? lease, bool authorize, CancellationToken ct,
        IReadOnlyList<Guid>? additionalSubjects = null)
    {
        var hint = await turns.GetAsync(Owner, Access, id, false, ct);
        if (hint is null) return AssistantErrors.TurnNotFound;
        var original = await durable.GetByIdAsync(Owner, Access, hint.BindingId, ct);
        if (original?.SnapshotJson is null) return AssistantErrors.ConversationNotFound;
        var fencedSubjects = original.SubjectUserIds.Concat(additionalSubjects ?? []).Distinct().Order().ToArray();
        var ownerSafe = await privacy.LockAsync(Owner, fencedSubjects, ct);
        if (ownerSafe.IsFailure) return ownerSafe.Error;
        await durable.LockAsync(Owner, Access, Guid.Empty, hint.ConversationId, ct);
        var work = await turns.GetWorkAsync(id, true, ct);
        if (work is null || work.OwnerUserId != Owner || work.Access != Access || work.ScopeTenantId != tenant.TenantId) return AssistantErrors.TurnNotFound;
        if (work.IsTerminal || lease is not null && work.LeaseId != lease) return AssistantErrors.TurnInProgress;
        var turn = await turns.GetAsync(Owner, Access, id, true, ct);
        if (turn is null) return AssistantErrors.TurnNotFound;
        var bound = await durable.GetByIdAsync(Owner, Access, turn.BindingId, ct);
        if (bound?.SnapshotJson is null) return AssistantErrors.ConversationNotFound;
        if (bound.SubjectUserIds.Except(fencedSubjects).Any()) return AssistantErrors.TurnInProgress;
        var safeSubjects = await privacy.CheckAsync(Owner, fencedSubjects, ct);
        if (safeSubjects.IsFailure) return safeSubjects.Error;
        if (authorize)
        {
            var allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure) return allowed.Error;
        }
        return (turn, work);
    }
    private static void Append(IAssistantTurn turn, string kind, object data)
    {
        var events = AssistantTurnJson.Restore<List<AssistantTurnFrame>>(turn.EventsJson);
        events.Add(AssistantTurnJson.Frame(kind, turn.Sequence + 1, data));
        turn.Checkpoint(AssistantTurnJson.Serialize(events), turn.Sequence + 1);
    }
}
internal sealed record AssistantTurnSnapshot(AssistantTurnResultResponse Result, string? ErrorCode);
