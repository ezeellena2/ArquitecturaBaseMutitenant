using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Infrastructure.Idempotency;

/// <summary>Retira reservas vencidas; una falla temporal no detiene las demás rutas de la Api.</summary>
public sealed partial class IdempotencyCleanupWorker(
    IdempotencyStore store,
    TimeProvider timeProvider,
    ILogger<IdempotencyCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (NpgsqlException exception)
            {
                LogCleanupFailed(logger, exception);
            }

            await Task.Delay(Interval, timeProvider, stoppingToken);
        }
    }

    internal Task<int> CleanupOnceAsync(CancellationToken cancellationToken) =>
        store.CleanupExpiredAsync(cancellationToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Idempotency cleanup failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
