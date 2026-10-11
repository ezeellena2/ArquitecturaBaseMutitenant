using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantSnapshotRetentionService
{
    Task<Result<int>> PurgeExpiredAsync(AssistantSnapshotRetentionCandidate candidate, CancellationToken ct);
}
