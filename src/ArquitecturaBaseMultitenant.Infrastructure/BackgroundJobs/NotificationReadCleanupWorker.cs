using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>
/// Borra cada hora qué avisos de la campana se abrieron hace más de 90 días (base de la 12), en tandas y con un scope por
/// tanda, hasta que no queda ninguno o se llega al tope de la vuelta.
/// </summary>
internal sealed partial class NotificationReadCleanupWorker(IServiceScopeFactory scopes, TimeProvider timeProvider,
    ILogger<NotificationReadCleanupWorker> logger) : PeriodicWorker(TimeSpan.FromHours(1), timeProvider, logger)
{
    internal const int MaxBatchesPerRun = 20;

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        for (var batch = 0; batch < MaxBatchesPerRun && !ct.IsCancellationRequested; batch++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var removed = await scope.ServiceProvider.GetRequiredService<INotificationService>().CleanupExpiredReadsAsync(ct);
            if (removed.IsFailure)
            {
                LogFailed(Logger, removed.Error.Code);
                return;
            }
            if (removed.Value == 0) return;
        }
    }

    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification read cleanup failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
