using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

// Each store owns its classified queries; lifecycle and privacy rules remain in the repository.
internal abstract class AssistantTurnStore
{
    public abstract bool Handles(Access access);
    public abstract Task<AssistantTurnBase?> GetAsync(Guid owner, Access access, Guid id, bool forUpdate, CancellationToken ct);
    public abstract Task<Guid?> FindActiveAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct);
    public abstract Task<AssistantConversationBase?> GetConversationAsync(Guid owner, Access access, Guid id, uint? version, CancellationToken ct);
    public abstract Task<AssistantMessageBase?> GetMessageAsync(Guid owner, Access access, Guid conversationId, Guid messageId, CancellationToken ct);
    public abstract Task<AssistantMessageBase?> GetMessageMetadataAsync(Guid owner, Access access, IAssistantTurn turn, CancellationToken ct);
    public abstract Task<AssistantMessageBase[]> ListMessagesAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid>? ids, CancellationToken ct);
    public abstract Task<AssistantHistoryMessageRow[]> FindHistoryPrivacyAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct);
    public abstract Task<AssistantStoredAttachmentRow[]> ListAttachmentsAsync(Guid conversationId, CancellationToken ct);
    public abstract Task<int> CountUnboundAttachmentsAsync(Guid conversationId, IReadOnlyList<Guid> ids, CancellationToken ct);
    public abstract AssistantTurnBase CreateTurn(Guid conversationId, DateTime nowUtc);
    public abstract AssistantMessageBase CreateMessage(Guid conversationId, string role, string status, string? text, DateTime nowUtc);
    public abstract Task<AssistantDurableOperationBase?> GetBindingAsync(Guid owner, Access access, Guid id, CancellationToken ct);
    public abstract Task<AssistantTurnBase?> GetBindingTurnAsync(Guid bindingId, Guid conversationId, CancellationToken ct);
    public abstract Task<Guid[]> ListTurnIdsAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct);
}

internal sealed record AssistantStoredAttachmentRow(Guid Id, Guid? MessageId, string FileName, string ContentType, long Size, uint Version);
