using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantTurnRequester(IAssistantTurnRepository turns, IAssistantDurableRepository durable,
    IIdempotentRequestContext requests, AssistantConversationPolicy policy, IAssistantConfigurationPolicy configuration,
    AssistantLimitGuard limits, AssistantSnapshotPrivacyGuard privacy, AssistantTurnPreparationBuilder preparation, TimeProvider clock)
{
    internal Task<Result<AssistantTurnPreparation>> PrepareAsync(Guid? conversationId, CancellationToken ct) => preparation.BuildAsync(conversationId, ct);
    internal async Task<Result<AssistantTurnAdmission>> AdmitAsync(Guid conversationId, SendAssistantMessageRequest input,
        AssistantTurnPreparation prepared, CancellationToken ct)
    {
        var actor = prepared.Actor;
        var request = requests.Request ?? throw new InvalidOperationException("A durable request is required.");
        var protectedOwner = await privacy.LockAsync(actor.OwnerUserId, prepared.Subjects, ct);
        if (protectedOwner.IsFailure) return protectedOwner.Error;
        await durable.LockAsync(actor.OwnerUserId, actor.Access, request.Key, conversationId, ct);
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        if (await durable.GetByKeyAsync(actor.OwnerUserId, actor.Access, request.Key, ct) is { } existing)
            return existing.BodyHash != request.BodyHash || existing.Route != request.Route ? RequestErrors.IdempotencyKeyReused : RequestErrors.InProgress;
        var conversation = await turns.GetConversationAsync(actor.OwnerUserId, actor.Access, conversationId, input.ConversationVersion, ct);
        if (conversation is null) return AssistantErrors.ConversationNotFound;
        if (conversation.Version != input.ConversationVersion) throw new ConcurrencyConflictException();
        if (await turns.FindActiveAsync(actor.OwnerUserId, actor.Access, conversationId, ct) is not null) return AssistantErrors.TurnInProgress;
        var attachmentIds = input.AttachmentIds ?? [];
        if (!await turns.ValidateAttachmentsAsync(actor.OwnerUserId, actor.Access, conversationId, attachmentIds, ct)) return AssistantErrors.AttachmentNotFound;
        var history = await turns.FindHistoryPrivacyAsync(actor.OwnerUserId, actor.Access, conversationId, ct);
        if (!AssistantTurnPreparationBuilder.HasKnownProvenance(history)) return AssistantErrors.ConversationNotFound;
        var subjects = history.SelectMany(message => message.SubjectUserIds).Distinct().Order().ToArray();
        // A changed context requires a fresh preflight; never acquire new account shares after conversation locks.
        if (!history.Select(message => message.Id).SequenceEqual(prepared.History.Select(message => message.Id)) ||
            !subjects.SequenceEqual(prepared.Subjects)) return RequestErrors.InProgress;
        var safe = await privacy.CheckAsync(actor.OwnerUserId, subjects, ct);
        if (safe.IsFailure) return safe.Error;
        var resolved = await configuration.ResolveAsync(ct);
        if (resolved.IsFailure) return resolved.Error;
        var config = resolved.Value;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var turn = turns.Add(conversationId, actor.Access, nowUtc);
        var reserved = await limits.ReserveAsync(new AssistantFinanceAdmission(turn.Id,
            new AssistantFinancialBinding(actor.OwnerUserId, actor.Access, actor.TenantId), config.Mode, config.DailyMessageLimit,
            config.MonthlyBudget, prepared.OrganizationTimeZoneId, AssistantPriceSnapshotBuilder.Build(config.Model, config.Reference),
            conversation.Title is null && history.Count == 0 ? config.TitlePrice : null), ct);
        if (reserved.IsFailure) return reserved.Error;
        var userMessage = turns.AddMessage(conversationId, actor.Access, "User", "Complete", input.Text, nowUtc);
        await turns.BindAttachmentsAsync(actor.OwnerUserId, actor.Access, conversationId, userMessage, attachmentIds, ct);
        var assistantMessage = turns.AddMessage(conversationId, actor.Access, "Assistant", "Generating", null, nowUtc.AddTicks(10), subjects);
        var reference = new IdempotentReference("assistant.turn", Guid.Empty, 200, null);
        var binding = durable.Add(actor.OwnerUserId, actor.Access, request, conversationId, reference,
            AssistantTurnJson.Serialize(turn.Id), null, nowUtc, subjects);
        turn.Bind(userMessage, assistantMessage, binding.Id);
        turn.CaptureContext(prepared.History.Select(message => message.Id).Append(userMessage).ToArray());
        turns.Touch(conversation, nowUtc);
        turns.Add(AssistantTurnWork.Create(turn.Id, actor.OwnerUserId, actor.Access, actor.TenantId, actor.TenantKind,
            actor.Culture, actor.TimeZoneId, actor.SessionStartedAtUtc, config.Model.Id, config.Model.ProviderModelId,
            config.Effort.ToString(), reserved.Value.Funding, nowUtc, nowUtc.Add(prepared.TurnTimeout)));
        return new AssistantTurnAdmission(reference with { Id = binding.Id }, turn.Id);
    }
    internal async Task<Result> StopAsync(Guid conversationId, Guid turnId, AssistantTurnPreparation prepared, CancellationToken ct)
    {
        var actor = prepared.Actor;
        await durable.LockAsync(actor.OwnerUserId, actor.Access, Guid.Empty, conversationId, ct);
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed;
        var turn = await turns.GetAsync(actor.OwnerUserId, actor.Access, turnId, true, ct);
        if (turn is null || turn.ConversationId != conversationId) return AssistantErrors.TurnNotFound;
        var work = await turns.GetWorkAsync(turnId, true, ct);
        if (work is null) return AssistantErrors.TurnNotFound;
        if (!work.IsTerminal) work.RequestStop();
        return Result.Success();
    }
    internal Task<Guid?> ActiveAsync(Guid owner, Domain.Users.Access access, Guid id, CancellationToken ct) => turns.FindActiveAsync(owner, access, id, ct);
}
