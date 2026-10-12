using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

public interface IAssistantAttachmentContentReader
{
    Task<Result<IReadOnlyList<AssistantAttachmentContent>>> ReadAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid> attachmentIds, CancellationToken ct);
}
