using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantRetentionService
{
    Task<Result<int>> PurgeAsync(AssistantConversationRetentionRow candidate, DateTime cutoffUtc, CancellationToken ct);
    Task<Result> CompleteCleanupAsync(Guid id, CancellationToken ct);
}
