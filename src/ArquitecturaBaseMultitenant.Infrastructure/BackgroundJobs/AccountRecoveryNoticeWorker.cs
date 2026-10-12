using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>
/// Encola, fuera de la respuesta de «Recuperar mi cuenta», el aviso a la cuenta encontrada por el método de antes
/// (ADR 0041). Cada tanda va en su propio scope y su transacción.
/// </summary>
internal sealed partial class AccountRecoveryNoticeWorker(IServiceScopeFactory scopes, TimeProvider timeProvider,
    ILogger<AccountRecoveryNoticeWorker> logger) : PeriodicWorker(Interval, timeProvider, logger)
{
    /// <summary>El aviso es de seguridad: sale enseguida, sin atar el tiempo del pedido a si la cuenta existe.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var notified = await scope.ServiceProvider.GetRequiredService<IAccountRecoveryNoticeService>()
                .NotifyAccountsAsync(ct);
            if (notified.IsFailure)
            {
                LogFailed(Logger, notified.Error.Code);
                break;
            }

            if (notified.Value == 0) break;
        }
    }

    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);

    [LoggerMessage(Level = LogLevel.Error, Message = "Account recovery notices failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
