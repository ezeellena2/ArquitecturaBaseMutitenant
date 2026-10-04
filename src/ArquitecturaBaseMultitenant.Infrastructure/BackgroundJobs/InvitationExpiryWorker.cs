using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>
/// Registra en la auditoría de cada organización activa las invitaciones que vencieron pendientes, una sola vez y con
/// la fecha en que vencieron. Cada organización va en su propio scope, con <see cref="TenantJobRunner"/>, y cada tanda
/// en su transacción. Una organización que falla no frena a las demás.
/// </summary>
internal sealed partial class InvitationExpiryWorker(TenantJobRunner runner, TimeProvider timeProvider,
    ILogger<InvitationExpiryWorker> logger) : BackgroundService
{
    /// <summary>La entrada lleva la fecha real del vencimiento: la frecuencia solo cambia cuándo aparece.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    internal Task RunOnceAsync(CancellationToken ct) => runner.RunActiveBusinessesAsync(async (services, token) =>
    {
        try
        {
            var expiries = services.GetRequiredService<IInvitationExpiryService>();
            while (!token.IsCancellationRequested)
            {
                var recorded = await expiries.RecordExpiredAsync(token);
                if (recorded.IsFailure)
                {
                    LogFailed(logger, recorded.Error.Code);
                    break;
                }

                if (recorded.Value == 0) break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) { LogFailed(logger, exception.GetType().Name); }
    }, ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                LogFailed(logger, exception.GetType().Name);
                try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Invitation expiry recording failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
