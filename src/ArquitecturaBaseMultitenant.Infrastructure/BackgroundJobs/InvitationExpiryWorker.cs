using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>
/// Registra en la auditoría de cada organización activa las invitaciones que vencieron pendientes, una sola vez y con
/// la fecha en que vencieron. Cada organización va en su propio scope, con <see cref="TenantJobRunner"/>, y cada tanda
/// en su transacción. Una organización que falla no frena a las demás.
/// </summary>
internal sealed partial class InvitationExpiryWorker(TenantJobRunner runner, TimeProvider timeProvider,
    ILogger<InvitationExpiryWorker> logger) : PeriodicWorker(Interval, timeProvider, logger)
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
                    LogFailed(Logger, recorded.Error.Code);
                    break;
                }

                if (recorded.Value == 0) break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) { ExceptionLog.Write(Logger, nameof(InvitationExpiryWorker), exception); }
    }, ct);

    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);

    [LoggerMessage(Level = LogLevel.Error, Message = "Invitation expiry recording failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
