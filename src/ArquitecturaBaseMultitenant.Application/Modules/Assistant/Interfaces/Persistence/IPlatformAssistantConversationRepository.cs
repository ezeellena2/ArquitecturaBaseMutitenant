using ArquitecturaBaseMultitenant.Domain.Assistant;
namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
public interface IPlatformAssistantConversationRepository
{
    void Add(PlatformAssistantConversation conversation);
    Task<PlatformAssistantConversation?> GetByIdAsync(Guid ownerUserId, Guid conversationId, uint? expectedVersion, CancellationToken ct);
}
