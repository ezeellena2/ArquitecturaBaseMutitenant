using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantAttachmentRepository
{
    Task<AssistantAttachmentRow?> GetAsync(Guid owner, Access access, Guid conversationId, Guid attachmentId, CancellationToken ct);
    Task<IReadOnlyList<AssistantAttachmentRow>> ListAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid> ids, CancellationToken ct);
    IVersioned Add(Access access, Guid conversationId, string fileName, string mediaType, string storageKey, long length, DateTime nowUtc);
    Task RemoveAsync(Guid owner, Access access, AssistantAttachmentRow attachment, uint expectedVersion, DateTime nowUtc, CancellationToken ct);
    Task BindAsync(Guid owner, Access access, Guid conversationId, Guid messageId, IReadOnlyList<Guid> ids, CancellationToken ct);
}
