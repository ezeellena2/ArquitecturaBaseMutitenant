using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSnapshotRetentionService(IAssistantSnapshotRetentionRepository repository, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<AssistantSnapshotRetentionService> logger) : IAssistantSnapshotRetentionService
{
    public Task<Result<int>> PurgeExpiredAsync(AssistantSnapshotRetentionCandidate candidate, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RetainAssistantSnapshot", () => unitOfWork.ExecuteInTransactionAsync(async c =>
            Result.Success(await repository.PurgeExpiredAsync(candidate, clock.GetUtcNow().UtcDateTime, c)), CommitPolicy.OnSuccess, ct));
}
