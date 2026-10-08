using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
namespace ArquitecturaBaseMultitenant.Application.Services.Messaging;

/// <summary>Un límite por fase durable; nunca envía ni descifra payloads.</summary>
internal sealed class OutboxDispatchService(IUnitOfWork unitOfWork, IOutboxDispatchStore store,
    IEnumerable<IOutboxDeliveryObserver> observers, TimeProvider clock, ILogger<OutboxDispatchService> logger) : IOutboxDispatchService
{
    public Task<Result<OutboxDispatchClaim?>> ClaimNextAsync(IReadOnlyCollection<string> channels, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ClaimOutbox", async () =>
        {
            ArgumentNullException.ThrowIfNull(channels);
            return await unitOfWork.ExecuteInTransactionAsync(async c =>
                Result.Success(await store.ClaimNextAsync(channels, c)), CommitPolicy.OnSuccess, ct);
        });

    public Task<Result> CompleteAsync(Guid messageId, Guid leaseId, OutboxDeliveryReceipt receipt, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "CompleteOutbox", () =>
            unitOfWork.ExecuteInTransactionAsync(async c =>
            {
                ArgumentNullException.ThrowIfNull(receipt);
                var channel = await store.CompleteAsync(messageId, leaseId, receipt, c);
                if (channel is not null && receipt.IsAccepted)
                    foreach (var observer in observers.Where(value => value.Key == channel))
                        await observer.RecordAcceptedAsync(messageId, receipt, clock.GetUtcNow().UtcDateTime, c);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct));
}
