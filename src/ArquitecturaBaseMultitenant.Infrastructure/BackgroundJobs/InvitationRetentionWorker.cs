using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>
/// ADR 0062: una vez por día borra las invitaciones cerradas vencidas de plazo de cada organización, también de una
/// suspendida o cerrada (la retención no depende de que esté activa), una por vez en su propio alcance, con
/// <see cref="TenantJobRunner"/>: sin un rol que saltee el aislamiento. Cada lote va en su transacción. Una organización
/// que falla no frena a las demás, y el log dice cuántas borró o solo el tipo de la excepción.
/// </summary>
internal sealed partial class InvitationRetentionWorker(TenantJobRunner runner, TimeProvider timeProvider,
    ILogger<InvitationRetentionWorker> logger) : PeriodicWorker(TimeSpan.FromDays(1), timeProvider, logger)
{
    internal Task RunOnceAsync(CancellationToken ct) => runner.RunBusinessesAsync(PurgeOrganizationAsync, ct);

    /// <summary>Las de la organización del scope, lote por lote hasta recorrerlas todas.</summary>
    internal async Task PurgeOrganizationAsync(IServiceProvider services, CancellationToken ct)
    {
        try
        {
            var retention = services.GetRequiredService<IInvitationRetentionService>();
            var total = 0;
            RetentionCursor? after = null;
            do
            {
                var batch = await retention.PurgeClosedAsync(after, ct);
                if (batch.IsFailure)
                {
                    LogFailed(Logger, batch.Error.Code);
                    break;
                }

                total += batch.Value.Removed;
                after = batch.Value.Next;
            }
            while (after is not null && !ct.IsCancellationRequested);
            if (total > 0) LogRemoved(Logger, total);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception) { ExceptionLog.Write(Logger, nameof(InvitationRetentionWorker), exception); }
    }

    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);

    [LoggerMessage(Level = LogLevel.Information, Message = "Invitation retention removed {Count} closed invitations")]
    private static partial void LogRemoved(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Invitation retention failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
