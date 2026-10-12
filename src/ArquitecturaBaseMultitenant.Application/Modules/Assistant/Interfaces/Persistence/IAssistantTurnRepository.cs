using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantTurnRepository
{
    Task<IReadOnlyList<AssistantTurnDiscovery>> ListReadyAsync(DateTime nowUtc, CancellationToken ct);
    Task<AssistantTurnWork?> GetWorkAsync(Guid turnId, bool forUpdate, CancellationToken ct);
    Task<IAssistantTurn?> GetAsync(Guid owner, Access access, Guid turnId, bool forUpdate, CancellationToken ct);
    Task<Guid?> FindActiveAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct);
    Task<IAssistantConversation?> GetConversationAsync(Guid owner, Access access, Guid id, uint? version, CancellationToken ct);
    Task<IReadOnlyList<AssistantHistoryMessageRow>> FindHistoryPrivacyAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct);
    Task<IReadOnlyList<AssistantMessageResponse>> ListMessagesAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct, IReadOnlyList<Guid>? messageIds = null);
    Task<bool> ValidateAttachmentsAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid> ids, CancellationToken ct);
    Task BindAttachmentsAsync(Guid owner, Access access, Guid conversationId, Guid messageId, IReadOnlyList<Guid> ids, CancellationToken ct);
    IAssistantTurn Add(Guid conversationId, Access access, DateTime nowUtc);
    Guid AddMessage(Guid conversationId, Access access, string role, string status, string? text, DateTime nowUtc, IReadOnlyCollection<Guid>? subjects = null);
    void Add(AssistantTurnWork work);
    void Touch(IAssistantConversation conversation, DateTime nowUtc);
    Task CheckpointMessageAsync(Guid owner, Access access, IAssistantTurn turn, string? text, string? status, CancellationToken ct);
    Task FinishMessageStatusAsync(Guid owner, Access access, IAssistantTurn turn, string status, CancellationToken ct);
    Task AddSubjectsAsync(Guid owner, Access access, Guid bindingId, IReadOnlyList<Guid> subjects, CancellationToken ct);
    Task SetSnapshotAsync(Guid owner, Access access, Guid bindingId, string? snapshot, CancellationToken ct);
    Task CancelConversationAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct);
}
