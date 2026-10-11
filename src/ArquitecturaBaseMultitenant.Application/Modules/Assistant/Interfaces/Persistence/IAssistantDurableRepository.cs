using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantDurableRepository
{
    Task LockAsync(Guid owner, Access access, Guid key, Guid? conversationId, CancellationToken ct);
    Task<AssistantDurableOperationRow?> GetByKeyAsync(Guid owner, Access access, Guid key, CancellationToken ct);
    Task<AssistantDurableOperationRow?> GetByIdAsync(Guid owner, Access access, Guid id, CancellationToken ct);
    Entity Add(Guid owner, Access access, IdempotentRequest request, Guid conversationId,
        IdempotentReference reference, string snapshot, string? quickKey, DateTime nowUtc, Guid[]? subjectUserIds = null);
    Task RemoveContentAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct);
    Task StageFileCleanupAsync(Guid owner, Access access, Guid conversationId, DateTime nowUtc, CancellationToken ct);
}
