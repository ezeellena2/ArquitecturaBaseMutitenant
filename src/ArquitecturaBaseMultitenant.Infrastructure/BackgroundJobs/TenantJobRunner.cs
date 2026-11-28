using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>Ejecuta trabajo técnico en un scope independiente por organización.</summary>
public sealed class TenantJobRunner(IServiceScopeFactory scopeFactory, ILogger<TenantJobRunner> logger)
{
    private static readonly Func<ILogger, Guid, IDisposable?> BeginTenantScope =
        LoggerMessage.DefineScope<Guid>("TenantId:{TenantId}");

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

    /// <summary>
    /// Recorre todas las organizaciones, también las suspendidas y las cerradas: para la retención de datos personales,
    /// que no depende de que la organización esté activa (ADR 0062).
    /// </summary>
    public async Task RunBusinessesAsync(
        Func<IServiceProvider, CancellationToken, Task> job,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        IReadOnlyList<TenantRow> businesses;
        await using (var listingScope = scopeFactory.CreateAsyncScope())
        {
            businesses = await listingScope.ServiceProvider.GetRequiredService<ITenantReader>()
                .ListBusinessesAsync(cancellationToken);
        }

        await RunAsync(businesses.Select(tenant => tenant.Id), job, cancellationToken);
    }

    /// <summary>Ejecuta el trabajo en un scope propio de una organización y propaga cualquier falla al llamador.</summary>
    public async Task RunForTenantAsync(
        Guid tenantId,
        Func<IServiceProvider, CancellationToken, Task> job,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        await job(scope.ServiceProvider, cancellationToken);
    }

    /// <summary>
    /// Recorre cada organización en su propio scope. Una falla técnica se registra y no frena a las demás; la
    /// cancelación pedida en <paramref name="cancellationToken"/> sí se propaga.
    /// </summary>
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
            try
            {
                await RunForTenantAsync(tenantId, job, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                using var _ = BeginTenantScope(logger, tenantId);
                ExceptionLog.Write(logger, nameof(TenantJobRunner), ex);
            }
        }
    }
}
