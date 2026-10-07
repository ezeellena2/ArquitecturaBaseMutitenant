using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Reclama bajas vencidas y procesa cada tenant en una transacción con alcance propio antes del cierre global.</summary>
internal sealed class AccountDeletionProcessingService(IAccountDeletionRepository repository, IAccountDeletionTenantReader tenants,
    AccountDeletionProcessor processor, ITenantScope tenantScope, IConsumerDataRetentionReader retentions,
    IConsumerDataRetentionRepository retentionRepository, IConsumerCopyDeletion consumerCopies, IUnitOfWork unitOfWork,
    TimeProvider timeProvider, ILogger<AccountDeletionProcessingService> logger) : IAccountDeletionProcessingService
{
    public Task<Result<AccountDeletionWork?>> ClaimNextAsync(CancellationToken ct) =>
        OperationLog.RunAsync<AccountDeletionWork?>(logger, timeProvider, "ClaimAccountDeletion", () =>
            unitOfWork.ExecuteInTransactionAsync<Result<AccountDeletionWork?>>(async innerCt =>
            {
                var leaseId = Guid.CreateVersion7();
                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                var userId = await repository.ClaimAsync(leaseId, nowUtc, nowUtc.AddMinutes(15), innerCt);
                if (userId is null) return Result.Success<AccountDeletionWork?>(null);
                return new AccountDeletionWork(userId.Value, leaseId, await tenants.ListAsync(userId.Value, innerCt));
            }, CommitPolicy.OnSuccess, ct));

    public Task<Result> ProcessTenantAsync(AccountDeletionWork work, AccountDeletionTenant tenant, CancellationToken ct) =>
        OperationLog.RunAsync(logger, timeProvider, "ProcessAccountDeletionTenant", async () =>
        {
            if (!work.Tenants.Contains(tenant)) throw new InvalidOperationException("The tenant must belong to the claimed work.");
            using var scope = tenantScope.Enter(tenant.TenantId);
            var result = await unitOfWork.ExecuteInTransactionAsync(
                innerCt => processor.ProcessTenantAsync(work, tenant, timeProvider.GetUtcNow().UtcDateTime, innerCt), CommitPolicy.OnSuccess, ct);
            if (result.IsSuccess) await processor.InvalidateAsync(work, tenant, ct);
            return result;
        });

    public Task<Result<IReadOnlyList<ConsumerDataRetentionWork>>> ListExpiredRetentionsAsync(CancellationToken ct) =>
        OperationLog.RunAsync<IReadOnlyList<ConsumerDataRetentionWork>>(logger, timeProvider, "ListExpiredConsumerRetentions", async () =>
            Result.Success(await retentions.ListExpiredAsync(timeProvider.GetUtcNow().UtcDateTime, 100, ct)));

    public Task<Result> ProcessRetentionAsync(ConsumerDataRetentionWork work, CancellationToken ct) =>
        OperationLog.RunAsync(logger, timeProvider, "ProcessConsumerRetention", async () =>
        {
            using var scope = tenantScope.Enter(work.ConsumerTenantId);
            return await unitOfWork.ExecuteInTransactionAsync(async innerCt =>
            {
                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                var retention = await retentionRepository.GetForProcessingAsync(work, nowUtc, innerCt);
                if (retention is null) return Result.Success();
                await consumerCopies.ReplaceAsync(retention.EntityTypeKey, retention.EntityId,
                    retention.ConsumerTenantId, retention.BusinessTenantId, innerCt);
                retention.MarkCopyReplaced(nowUtc);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        });

    public Task<Result> CompleteAsync(AccountDeletionWork work, CancellationToken ct) =>
        OperationLog.RunAsync(logger, timeProvider, "CompleteAccountDeletion", async () =>
        {
            var result = await unitOfWork.ExecuteInTransactionAsync(
                innerCt => processor.CompleteAsync(work, timeProvider.GetUtcNow().UtcDateTime, innerCt), CommitPolicy.OnSuccess, ct);
            if (result.IsSuccess) await processor.InvalidateAsync(work, null, ct);
            return result;
        });
}
