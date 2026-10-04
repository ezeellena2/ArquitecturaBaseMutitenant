using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Toma los pedidos de «Recuperar mi cuenta» cuyo aviso a la cuenta no se resolvió, avisa a la cuenta encontrada (si la
/// hay) y los marca, en una transacción por tanda. Su categoría de logs queda en Warning, como el outbox, porque corre
/// cada 30 segundos.
/// </summary>
internal sealed class AccountRecoveryNoticeService(
    IAccountRecoveryRepository recoveries,
    AccountRecoveryNotifier messenger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AccountRecoveryNoticeService> logger) : IAccountRecoveryNoticeService
{
    private const int BatchSize = 50;

    public Task<Result<int>> NotifyAccountsAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "NotifyAccountRecoveryAccounts", () =>
            unitOfWork.ExecuteInTransactionAsync<Result<int>>(async ct =>
            {
                var pending = await recoveries.ListAwaitingAccountNoticeAsync(BatchSize, ct);
                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                foreach (var recovery in pending)
                {
                    if (recovery.UserId is { } userId)
                        await messenger.NotifyAccountAsync(userId, recovery.CreatedAtUtc, ct);
                    recovery.MarkAccountNotified(nowUtc);
                }

                return pending.Count;
            }, CommitPolicy.OnSuccess, cancellationToken));
}
