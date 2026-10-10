using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Users;
namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
public interface IAssistantConversationReader
{
    Task<AssistantConversationSummaryResponse?> FindByIdAsync(Guid ownerUserId, Access access, Guid conversationId, CancellationToken ct);
    Task<CursorResult<AssistantConversationSummaryResponse>> ListAsync(Guid ownerUserId, Access access, AssistantConversationListRequest request, CancellationToken ct);
    Task<CursorResult<AssistantMessageResponse>> ListMessagesAsync(Guid ownerUserId, Access access, Guid conversationId, AssistantMessageListRequest request, CancellationToken ct);
    Task<bool> ExistsAttachmentAsync(Guid ownerUserId, Access access, Guid conversationId, Guid attachmentId, CancellationToken ct);
}
