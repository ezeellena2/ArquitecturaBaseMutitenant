using System.Data;
using System.Data.Common;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>
/// ADR 0068 §5: cada herramienta o fuente corre en un alcance de DI nuevo, con su propio contexto de EF. En el worker entra con
/// el mismo actor que el worker ya revalidó; en una petición, con la organización que fijó su acceso (el claim, ya comprobado
/// por el middleware) y la persona de su identidad firmada. Nunca hereda un <c>ITenantScope.Enter</c> técnico ni lo usa. La
/// transacción es <c>READ ONLY</c> y siempre se deshace: lo que quede en memoria se descarta con el alcance.
/// </summary>
internal sealed partial class AssistantReadOnlyScope(IServiceScopeFactory scopes, IExecutionActorContext actors,
    TenantContext tenant, ILogger<AssistantReadOnlyScope> logger) : IAssistantReadOnlyScope
{
    public async Task<TResult> RunAsync<TService, TResult>(Func<TService, CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken) where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(work);
        var actor = actors.Actor;
        if (actor is null && tenant.HasTechnicalScope)
            throw new InvalidOperationException("The assistant read-only scope never inherits a technical tenant scope.");
        var access = actor is null ? tenant.AccessBinding : null;
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var entered = actor is null ? null : services.GetRequiredService<IExecutionActorScope>().Enter(actor);
        if (access is { } bound) services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(bound.TenantId, bound.Kind);
        var db = services.GetRequiredService<ApplicationDbContext>();
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // Primera sentencia de la transacción: desde acá PostgreSQL rechaza cualquier escritura (SQLSTATE 25006).
            await db.Database.ExecuteSqlAsync($"SET TRANSACTION READ ONLY", cancellationToken);
            var tenantId = services.GetRequiredService<ITenantContext>().TenantId?.ToString("D") ?? string.Empty;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.tenant_id', {tenantId}, true)", cancellationToken);
            return await work(services.GetRequiredService<TService>(), cancellationToken);
        }
        finally
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception exception) when (exception is DbException or InvalidOperationException)
            {
                LogRollbackFailed(logger, exception.GetType().Name);
            }
            finally
            {
                db.ChangeTracker.Clear();
                await transaction.DisposeAsync();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rolling back the assistant read-only scope failed with {ExceptionType}")]
    private static partial void LogRollbackFailed(ILogger logger, string exceptionType);
}
