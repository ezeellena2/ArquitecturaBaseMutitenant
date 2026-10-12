using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Idempotency;

/// <summary>Retira reservas vencidas; una falla temporal no detiene las demás rutas de la Api.</summary>
public sealed class IdempotencyCleanupWorker(
    IIdempotencyStore store,
    TimeProvider timeProvider,
    ILogger<IdempotencyCleanupWorker> logger) : PeriodicWorker(TimeSpan.FromHours(1), timeProvider, logger)
{
    protected override Task RunIterationAsync(CancellationToken ct) => CleanupOnceAsync(ct);

    internal Task<int> CleanupOnceAsync(CancellationToken cancellationToken) =>
        store.CleanupExpiredAsync(cancellationToken);

}
