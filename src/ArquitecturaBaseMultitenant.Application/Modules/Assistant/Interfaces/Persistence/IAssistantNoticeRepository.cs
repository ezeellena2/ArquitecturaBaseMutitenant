using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantNoticeRepository
{
    Task LockAsync(Guid ownerUserId, CancellationToken ct);
    void Add(AssistantNoticeAcceptance acceptance);
}
