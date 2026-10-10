using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed record AssistantDurableWrite(IdempotentReference Reference, AssistantDurableSnapshot Snapshot, IVersioned? Row)
{
    internal AssistantDurableSnapshot AfterSave() => Row is null ? Snapshot : Snapshot with
    {
        Conversation = Snapshot.Conversation with { Version = Row.Version },
        Messages = Snapshot.Messages.Select(message => message with { Version = Row.Version }).ToArray(),
    };
}

internal sealed class AssistantConversationWriter(ICurrentUser user, IAssistantConversationRepository repository,
    IPlatformAssistantConversationRepository platform, IAssistantDurableRepository durable,
    IIdempotentRequestContext context, AssistantConversationPolicy policy, IAssistantQuickService quick, TimeProvider clock)
{
    internal async Task<Result<AssistantDurableWrite>> CreateAsync(CancellationToken ct)
    {
        var request = context.Request ?? throw new InvalidOperationException("A durable request context is required.");
        await durable.LockAsync(Owner, Access, request.Key, null, ct);
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        if (await durable.GetByKeyAsync(Owner, Access, request.Key, ct) is { } existing) return await ExistingAsync(request, existing, ct);
        var row = Create();
        var location = request.Origin + request.Route.Split('?')[0] + "/" + row.Id.ToString("D");
        var snapshot = new AssistantDurableSnapshot(AssistantSnapshotBuilder.Summary(row), [], null);
        return Stage(request, row, "assistant.conversation", 201, location, snapshot, null);
    }

    internal async Task<Result<AssistantDurableWrite>> QuickAsync(string key, AssistantQuickRequest input, CancellationToken ct)
    {
        var request = context.Request ?? throw new InvalidOperationException("A durable request context is required.");
        await durable.LockAsync(Owner, Access, request.Key, input.ConversationId, ct);
        var allowed = await policy.AuthorizeQuickAsync(key, [], ct);
        if (allowed.IsFailure) return allowed.Error;
        if (await durable.GetByKeyAsync(Owner, Access, request.Key, ct) is { } existing) return await ExistingAsync(request, existing, ct);
        IAssistantConversation? row = input.ConversationId is { } id ? await GetAsync(id, input.Version, ct) : null;
        if (input.ConversationId is not null && row is null) return AssistantErrors.ConversationNotFound;
        if (row is not null) CheckVersion(row, input.Version);
        var composition = await quick.ExecuteAsync(key, ct);
        if (composition.IsFailure) return composition.Error;
        allowed = await policy.AuthorizeQuickAsync(key, composition.Value.Result.Links, ct);
        if (allowed.IsFailure) return allowed.Error;
        row ??= Create();
        row.RecordQuick(composition.Value.Title, clock.GetUtcNow().UtcDateTime);
        if (input.ConversationId is not null) MarkUpdated(row);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var lastMessageAt = Access == Access.Platform ? await platform.LastMessageAtAsync(Owner, row.Id, ct)
            : await repository.LastMessageAtAsync(Owner, Access, row.Id, ct);
        // PostgreSQL stores microseconds. Serialize the pair and successive Quick calls under the conversation lock.
        nowUtc = new DateTime(nowUtc.Ticks - nowUtc.Ticks % 10, DateTimeKind.Utc);
        if (lastMessageAt is { } last && nowUtc <= last) nowUtc = last.AddTicks(10);
        var messages = new List<AssistantMessageResponse>();
        var question = AddMessage(row.Id, AssistantMessageRole.User, composition.Value.Title, [], nowUtc);
        messages.Add(question);
        messages.Add(AddMessage(row.Id, AssistantMessageRole.Assistant, composition.Value.Result.Text, composition.Value.Result.Links, nowUtc.AddTicks(10)));
        var snapshot = new AssistantDurableSnapshot(AssistantSnapshotBuilder.Summary(row), messages, composition.Value.Result);
        return Stage(request, row, "assistant.quick", 200, null, snapshot, key);
    }

    internal async Task<Result<IAssistantConversation>> UpdateAsync(Guid id, UpdateAssistantConversationRequest input, CancellationToken ct)
    {
        await durable.LockAsync(Owner, Access, Guid.Empty, id, ct);
        var row = await GetAsync(id, input.Version, ct);
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        if (row is null) return AssistantErrors.ConversationNotFound;
        CheckVersion(row, input.Version);
        row.Update(input.Title, input.IsSaved!.Value, clock.GetUtcNow().UtcDateTime);
        MarkUpdated(row);
        return Result.Success(row);
    }

    internal async Task<Result> DeleteAsync(Guid id, uint? version, CancellationToken ct)
    {
        await durable.LockAsync(Owner, Access, Guid.Empty, id, ct);
        var row = await GetAsync(id, version, ct);
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed;
        if (row is null) return AssistantErrors.ConversationNotFound;
        CheckVersion(row, version);
        await durable.StageFileCleanupAsync(Owner, Access, id, clock.GetUtcNow().UtcDateTime, ct);
        await durable.RemoveContentAsync(Owner, Access, id, ct);
        // No provider/worker/storage is admitted by this cut. Cascade removes owned messages and technical child work.
        if (row is PlatformAssistantConversation global) platform.Remove(global);
        else repository.Remove((AssistantConversation)row);
        return Result.Success();
    }

    internal async Task<Result<IVersioned>> FeedbackAsync(Guid id, Guid messageId, AssistantFeedbackRequest input, CancellationToken ct)
    {
        await durable.LockAsync(Owner, Access, Guid.Empty, id, ct);
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        if (await GetAsync(id, null, ct) is null) return AssistantErrors.ConversationNotFound;
        if (Access == Access.Platform)
        {
            var row = await platform.GetMessageAsync(Owner, id, messageId, input.Version, ct);
            if (row is null || row.Role != nameof(AssistantMessageRole.Assistant)) return AssistantErrors.ConversationNotFound;
            CheckVersion(row, input.Version);
            row.SetFeedback(input.Rating is null ? null : input.Rating == AssistantRating.Up);
            platform.MarkUpdated(row);
            return Result.Success<IVersioned>(row);
        }
        else
        {
            var row = await repository.GetMessageAsync(Owner, Access, id, messageId, input.Version, ct);
            if (row is null || row.Role != nameof(AssistantMessageRole.Assistant)) return AssistantErrors.ConversationNotFound;
            CheckVersion(row, input.Version);
            row.SetFeedback(input.Rating is null ? null : input.Rating == AssistantRating.Up);
            repository.MarkUpdated(row);
            return Result.Success<IVersioned>(row);
        }
    }

    private Guid Owner => user.UserId ?? throw new InvalidOperationException("A session is required.");
    private Access Access => user.Access ?? throw new InvalidOperationException("An access is required.");
    private IAssistantConversation Create()
    {
        if (Access == Access.Platform)
        {
            var row = PlatformAssistantConversation.Create(Owner, clock.GetUtcNow().UtcDateTime);
            platform.Add(row);
            return row;
        }
        var privateRow = AssistantConversation.Create(Owner, Access, clock.GetUtcNow().UtcDateTime);
        repository.Add(privateRow);
        return privateRow;
    }
    private async Task<IAssistantConversation?> GetAsync(Guid id, uint? version, CancellationToken ct) => Access == Access.Platform
        ? await platform.GetByIdAsync(Owner, id, version, ct) : await repository.GetByIdAsync(Owner, Access, id, version, ct);
    private void MarkUpdated(IAssistantConversation row)
    {
        if (row is PlatformAssistantConversation global) platform.MarkUpdated(global);
        else repository.MarkUpdated((AssistantConversation)row);
    }
    private static void CheckVersion(IVersioned row, uint? version)
    {
        if (version is { } expected && row.Version != expected) throw new ConcurrencyConflictException("The row changed since it was read.");
    }
    private AssistantMessageResponse AddMessage(Guid id, AssistantMessageRole role, string text, IReadOnlyList<AssistantLinkResponse> links, DateTime nowUtc)
    {
        Entity row;
        if (Access == Access.Platform)
        {
            var message = PlatformAssistantMessage.Create(id, role.ToString(), nameof(AssistantMessageStatus.Complete), text, nowUtc);
            message.SetLinks(JsonSerializer.Serialize(links));
            platform.Add(message);
            row = message;
        }
        else
        {
            var message = AssistantMessage.Create(id, role.ToString(), nameof(AssistantMessageStatus.Complete), text, nowUtc);
            message.SetLinks(JsonSerializer.Serialize(links));
            repository.Add(message);
            row = message;
        }
        return new AssistantMessageResponse(row.Id, role, text, AssistantMessageStatus.Complete, nowUtc, 0, [], links, null);
    }
    private AssistantDurableWrite Stage(IdempotentRequest request, IAssistantConversation conversation, string kind, int status,
        string? location, AssistantDurableSnapshot snapshot, string? quickKey)
    {
        var reference = new IdempotentReference(kind, Guid.Empty, status, location);
        var row = durable.Add(Owner, Access, request, conversation.Id, reference, AssistantSnapshotBuilder.Serialize(snapshot), quickKey, clock.GetUtcNow().UtcDateTime);
        return new AssistantDurableWrite(reference with { Id = row.Id }, snapshot, (IVersioned)row);
    }
    private async Task<Result<AssistantDurableWrite>> ExistingAsync(IdempotentRequest request, AssistantDurableOperationRow row, CancellationToken ct)
    {
        if (row.BodyHash != request.BodyHash || row.Route != request.Route) return RequestErrors.IdempotencyKeyReused;
        await durable.LockAsync(Owner, Access, Guid.Empty, row.ConversationId, ct);
        if (row.SnapshotJson is null || await GetAsync(row.ConversationId, null, ct) is null) return AssistantErrors.ConversationNotFound;
        var snapshot = AssistantSnapshotBuilder.Restore(row.SnapshotJson, row.Version);
        var allowed = row.QuickKey is { } quickKey ? await policy.AuthorizeQuickAsync(quickKey, snapshot.Result!.Links, ct) : await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        return new AssistantDurableWrite(row.Reference, snapshot, null);
    }
}
