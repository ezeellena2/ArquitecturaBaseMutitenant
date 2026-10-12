using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed record AssistantAttachmentWrite(IdempotentReference Reference, AssistantAttachmentSummaryResponse Summary, IVersioned? Row);

internal sealed class AssistantAttachmentWriter(ICurrentUser user, IAssistantConversationRepository privateConversations,
    IPlatformAssistantConversationRepository platformConversations, IAssistantDurableRepository durable, IAssistantAttachmentRepository attachments,
    IIdempotentRequestContext requestContext, AssistantConversationPolicy policy, AssistantSnapshotPrivacyGuard privacy, TimeProvider clock)
{
    internal async Task<Result<AssistantAttachmentWrite>> UploadAsync(Guid conversationId, AssistantUploadRequest input, string storageKey, CancellationToken ct)
    {
        var request = requestContext.Request ?? throw new InvalidOperationException("A durable request is required.");
        var safe = await privacy.LockAsync(Owner, [], ct);
        if (safe.IsFailure) return safe.Error;
        await durable.LockAsync(Owner, Access, request.Key, conversationId, ct);
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        if (await durable.GetByKeyAsync(Owner, Access, request.Key, ct) is { } existing)
        {
            if (existing.BodyHash != request.BodyHash || existing.Route != request.Route) return RequestErrors.IdempotencyKeyReused;
            var snapshot = existing.SnapshotJson is { } json ? AssistantSnapshotBuilder.Restore(json, existing.Version) : null;
            if (!AssistantSnapshotRetentionPolicy.HasContent(existing, clock.GetUtcNow().UtcDateTime) || snapshot?.Attachment is not { } attachment ||
                await attachments.GetAsync(Owner, Access, conversationId, attachment.Id, ct) is null) return AssistantErrors.AttachmentNotFound;
            return new AssistantAttachmentWrite(existing.Reference, attachment, null);
        }
        var conversation = await ConversationAsync(conversationId, input.ConversationVersion, ct);
        if (conversation is null) return AssistantErrors.ConversationNotFound;
        if (conversation.Version != input.ConversationVersion) throw new ConcurrencyConflictException("The conversation changed.");
        var row = attachments.Add(Access, conversationId, input.FileName!, input.MediaType!, storageKey, input.ByteLength!.Value, clock.GetUtcNow().UtcDateTime);
        var id = ((Entity)row).Id;
        var summary = new AssistantAttachmentSummaryResponse(id, input.FileName!, input.MediaType!, input.ByteLength.Value, 0);
        var snapshotNew = new AssistantDurableSnapshot(AssistantSnapshotBuilder.Summary(conversation), [], null) { Attachment = summary };
        var reference = new IdempotentReference("assistant.attachment", Guid.Empty, 201, request.Origin + request.Route.Split('?')[0] + "/" + id.ToString("D"));
        var binding = durable.Add(Owner, Access, request, conversationId, reference, AssistantSnapshotBuilder.Serialize(snapshotNew), null, clock.GetUtcNow().UtcDateTime);
        return new AssistantAttachmentWrite(reference with { Id = binding.Id }, summary, row);
    }

    internal async Task<Result> DeleteAsync(Guid conversationId, Guid attachmentId, uint version, CancellationToken ct)
    {
        var safe = await privacy.LockAsync(Owner, [], ct);
        if (safe.IsFailure) return safe;
        await durable.LockAsync(Owner, Access, Guid.Empty, conversationId, ct);
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed;
        var row = await attachments.GetAsync(Owner, Access, conversationId, attachmentId, ct);
        if (row is null) return AssistantErrors.AttachmentNotFound;
        if (row.MessageId is not null) return AssistantErrors.AttachmentAlreadyUsed;
        await attachments.RemoveAsync(Owner, Access, row, version, clock.GetUtcNow().UtcDateTime, ct);
        return Result.Success();
    }

    private Guid Owner => user.UserId ?? throw new InvalidOperationException("A session is required.");
    private Access Access => user.Access ?? throw new InvalidOperationException("An access is required.");
    private async Task<IAssistantConversation?> ConversationAsync(Guid id, uint? version, CancellationToken ct) => Access == Access.Platform
        ? await platformConversations.GetByIdAsync(Owner, id, version, ct) : await privateConversations.GetByIdAsync(Owner, Access, id, version, ct);
}
