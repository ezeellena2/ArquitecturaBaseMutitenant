using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantRetentionService(AssistantRetentionWriter writer, IAssistantRetentionRepository repository,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<AssistantRetentionService> logger) : IAssistantRetentionService
{
    public Task<Result<int>> PurgeAsync(AssistantConversationRetentionRow candidate, DateTime cutoffUtc, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationRetention", () => unitOfWork.ExecuteInTransactionAsync(async token =>
            Result.Success(await writer.PurgeAsync(candidate, cutoffUtc, token)), CommitPolicy.OnSuccess, ct));

    public Task<Result> CompleteCleanupAsync(Guid id, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantFileCleanupComplete", () => unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await repository.RemoveCleanupAsync(id, token);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct));
}
