using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Messaging;

internal sealed class OutboxDispatchService(
    IUnitOfWork unitOfWork,
    IOutboxDispatchStore store,
    TimeProvider timeProvider,
    ILogger<OutboxDispatchService> logger) : IOutboxDispatchService
{
    public Task<Result<int>> DispatchOnceAsync(IReadOnlyCollection<IChannelSender> senders,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "DispatchOutbox", async () =>
        {
            ArgumentNullException.ThrowIfNull(senders);
            if (senders.Count == 0)
            {
                return Result.Success(0);
            }

            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
                Result.Success(await store.DispatchDueAsync(senders, ct)),
                CommitPolicy.OnSuccess, cancellationToken);
        });
}
