using ArquitecturaBaseMultitenant.Domain.Assistant;
namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
public interface IPlatformAssistantConversationRepository
{
    void Add(PlatformAssistantConversation conversation);
    void Add(PlatformAssistantMessage message);
    void Remove(PlatformAssistantConversation conversation);
    Task<DateTime?> LastMessageAtAsync(Guid ownerUserId, Guid conversationId, CancellationToken ct);
    Task<PlatformAssistantMessage?> GetMessageAsync(Guid ownerUserId, Guid conversationId, Guid messageId, uint? expectedVersion, CancellationToken ct);
    void MarkUpdated(PlatformAssistantConversation conversation);
    void MarkUpdated(PlatformAssistantMessage message);
    Task<PlatformAssistantConversation?> GetByIdAsync(Guid ownerUserId, Guid conversationId, uint? expectedVersion, CancellationToken ct);
}
