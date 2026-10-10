using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
public interface IAssistantConversationRepository
{
    void Add(AssistantConversation conversation);
    void Add(AssistantMessage message);
    void Remove(AssistantConversation conversation);
    Task<DateTime?> LastMessageAtAsync(Guid ownerUserId, Access access, Guid conversationId, CancellationToken ct);
    Task<AssistantMessage?> GetMessageAsync(Guid ownerUserId, Access access, Guid conversationId, Guid messageId, uint? expectedVersion, CancellationToken ct);
    void MarkUpdated(AssistantConversation conversation);
    void MarkUpdated(AssistantMessage message);
    Task<AssistantConversation?> GetByIdAsync(Guid ownerUserId, Access access, Guid conversationId, uint? expectedVersion, CancellationToken ct);
}
