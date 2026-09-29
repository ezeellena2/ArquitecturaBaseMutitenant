using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>Ejecuta trabajo técnico en un scope independiente por organización.</summary>
public sealed class TenantJobRunner(IServiceScopeFactory scopeFactory)
{
    public async Task RunAsync(
        IEnumerable<Guid> tenantIds,
        Func<IServiceProvider, CancellationToken, Task> job,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        ArgumentNullException.ThrowIfNull(job);

        foreach (var tenantId in tenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = scopeFactory.CreateAsyncScope();
            using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
            await job(scope.ServiceProvider, cancellationToken);
        }
    }
}
