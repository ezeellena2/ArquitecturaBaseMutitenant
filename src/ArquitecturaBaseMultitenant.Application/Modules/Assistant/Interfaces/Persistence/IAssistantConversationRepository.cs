using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
public interface IAssistantConversationRepository
{
    void Add(AssistantConversation conversation);
    Task<AssistantConversation?> GetByIdAsync(Guid ownerUserId, Access access, Guid conversationId, uint? expectedVersion, CancellationToken ct);
}
