using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>Ejecuta trabajo técnico en un scope independiente por organización.</summary>
public sealed class TenantJobRunner(IServiceScopeFactory scopeFactory)
{
    /// <summary>Obtiene las organizaciones activas fuera de sus scopes de trabajo.</summary>
    public async Task RunActiveBusinessesAsync(
        Func<IServiceProvider, CancellationToken, Task> job,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        IReadOnlyList<TenantRow> active;
        await using (var listingScope = scopeFactory.CreateAsyncScope())
        {
            active = await listingScope.ServiceProvider.GetRequiredService<ITenantReader>()
                .ListActiveBusinessesAsync(cancellationToken);
        }

        await RunAsync(active.Select(tenant => tenant.Id), job, cancellationToken);
    }

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
