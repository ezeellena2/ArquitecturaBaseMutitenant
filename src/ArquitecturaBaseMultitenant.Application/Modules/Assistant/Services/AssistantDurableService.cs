using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantDurableService(ICurrentUser user, IAssistantDurableRepository repository,
    IAssistantConversationReader reader, AssistantConversationPolicy policy, AssistantSnapshotPrivacyGuard privacy, TimeProvider clock,
    ILogger<AssistantDurableService> logger) : IAssistantDurableService
{
    public Task<Result<IdempotentReference?>> ValidateAsync(IdempotentRequest request, string? quickKey, CancellationToken ct) =>
        OperationLog.RunAsync<IdempotentReference?>(logger, clock, "AssistantDurableValidate", async () =>
        {
            var allowed = quickKey is null ? await policy.AuthorizeAsync(ct) : await policy.AuthorizeQuickAsync(quickKey, [], ct);
            if (allowed.IsFailure) return allowed.Error;
            var row = await repository.GetByKeyAsync(user.UserId!.Value, user.Access!.Value, request.Key, ct);
            if (row is null) return Result.Success<IdempotentReference?>(null);
            if (row.BodyHash != request.BodyHash || row.Route != request.Route)
                return RequestErrors.IdempotencyKeyReused;
            return row.Reference;
        });

    public Task<Result<IdempotentReplayResponse>> ReplayAsync(IdempotentReference reference, CancellationToken ct) =>
        OperationLog.RunAsync<IdempotentReplayResponse>(logger, clock, "AssistantDurableReplay", async () =>
        {
            var allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure) return allowed.Error;
            var row = await repository.GetByIdAsync(user.UserId!.Value, user.Access!.Value, reference.Id, ct);
            if (row is null || row.Reference != reference || !AssistantSnapshotRetentionPolicy.HasContent(row, clock.GetUtcNow().UtcDateTime) ||
                await reader.FindByIdAsync(user.UserId.Value, user.Access.Value, row.ConversationId, ct) is null)
                return AssistantErrors.ConversationNotFound;
            var snapshot = AssistantSnapshotBuilder.Restore(row.SnapshotJson!, row.Version);
            if (row.QuickKey is { } quick)
            {
                var current = await policy.AuthorizeQuickAsync(quick, snapshot.Result!.Links, ct);
                if (current.IsFailure) return current.Error;
            }
            else
            {
                allowed = await policy.AuthorizeAsync(ct);
                if (allowed.IsFailure) return allowed.Error;
            }
            if (await reader.FindByIdAsync(user.UserId.Value, user.Access.Value, row.ConversationId, ct) is null)
                return AssistantErrors.ConversationNotFound;
            // GET/replay never opens a write boundary. Recheck privacy, persisted content and the clock after authorization awaits.
            var currentRow = await repository.GetByIdAsync(user.UserId.Value, user.Access.Value, reference.Id, ct);
            if (currentRow is null || currentRow.Reference != reference ||
                !AssistantSnapshotRetentionPolicy.HasContent(currentRow, clock.GetUtcNow().UtcDateTime))
                return AssistantErrors.ConversationNotFound;
            // Privacy is the final read: a claim during the durable reread must also reject the original.
            var safe = await privacy.CheckAsync(user.UserId.Value, currentRow.SubjectUserIds, ct);
            if (safe.IsFailure) return safe.Error;
            if (!AssistantSnapshotRetentionPolicy.HasContent(currentRow, clock.GetUtcNow().UtcDateTime))
                return AssistantErrors.ConversationNotFound;
            snapshot = AssistantSnapshotBuilder.Restore(currentRow.SnapshotJson!, currentRow.Version);
            if (currentRow.QuickKey is not null)
                return new IdempotentReplayResponse<AssistantQuickResponse>(reference,
                    new AssistantQuickResponse(snapshot.Conversation, snapshot.Messages, snapshot.Result!));
            return new IdempotentReplayResponse<AssistantConversationSummaryResponse>(reference, snapshot.Conversation);
        });
}
