using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantBudgetAlertService(IAssistantBudgetAlertRepository alerts, AssistantBudgetNotifier notifier,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<AssistantBudgetAlertService> logger) : IAssistantBudgetAlertService
{
    public Task<Result> RetryAsync(Guid id, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantBudgetRetry", () => unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            if (await alerts.ClaimAsync(id, c) is { } alert) await notifier.EnqueueAsync(alert, c);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct));
}
