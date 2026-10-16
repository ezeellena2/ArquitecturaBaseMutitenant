using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>Retira una vez por hora los contadores durables de cuotas con la ventana terminada (E9).</summary>
internal sealed class RateLimitCounterCleanupWorker(IServiceScopeFactory scopes, TimeProvider timeProvider,
    ILogger<RateLimitCounterCleanupWorker> logger) : PeriodicWorker(TimeSpan.FromHours(1), timeProvider, logger)
{
    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRateLimitService>().CleanupExpiredAsync(ct);
    }
}
