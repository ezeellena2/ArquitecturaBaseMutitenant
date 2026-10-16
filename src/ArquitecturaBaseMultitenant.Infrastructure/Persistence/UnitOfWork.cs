using System.Data;
using System.Data.Common;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Único límite de transacción y guardado de los casos de uso.</summary>
internal sealed partial class UnitOfWork(
    ApplicationDbContext dbContext,
    ITenantContext tenantContext,
    IRecentPlatformProofContext recentProofs,
    TimeProvider clock,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CommitPolicy policy,
        CancellationToken cancellationToken)
        where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(work);
        policy.ThrowIfUndefined();

        if (_transaction is not null)
        {
            throw new InvalidOperationException("A use case cannot nest UnitOfWork transactions.");
        }

        if (dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("A transaction from another owner is active in this scope.");
        }

        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        _transaction = transaction;

        try
        {
            var tenantId = tenantContext.TenantId?.ToString("D") ?? string.Empty;
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {tenantId}, true)", cancellationToken);
            if (recentProofs.RequiredSessionId is { } platformSessionId)
            {
                await RequireRecentProofAsync(platformSessionId, cancellationToken);
            }

            var result = await work(cancellationToken);
            if (!policy.Commits(result))
            {
                await RollbackAsync(transaction);
                return result;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            await RollbackAsync(transaction);

            if (exception is DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException("The row changed since it was read.", exception);
            }

            if (ConstraintViolations.Translate(exception) is { } violation)
            {
                throw violation;
            }

            throw;
        }
        finally
        {
            _transaction = null;
            await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// E9: una acción sensible de Plataforma vuelve a exigir, dentro de la transacción que escribe, la sesión vigente con
    /// el mismo sello y la prueba reciente. La sesión y la cuenta quedan bloqueadas para compartir hasta el commit: una
    /// revocación concurrente espera o ya ganó, y entonces la acción no se ejecuta.
    /// </summary>
    private async Task RequireRecentProofAsync(Guid platformSessionId, CancellationToken cancellationToken)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var proven = await dbContext.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM identity."PlatformSessions" s
            JOIN identity."AspNetUsers" u ON u."Id" = s."UserId"
            WHERE s."Id" = {platformSessionId} AND s."RevokedAtUtc" IS NULL
              AND s."RecentProofUntilUtc" > {nowUtc} AND s."SecurityStamp" = u."SecurityStamp"
            FOR SHARE OF s, u
            """).ToListAsync(cancellationToken);
        if (proven.Count == 0)
        {
            throw new PlatformReauthenticationRequiredException();
        }
    }

    private async Task RollbackAsync(IDbContextTransaction transaction)
    {
        try
        {
            // El token del pedido ya puede estar cancelado: los locks se sueltan igual.
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException)
        {
            LogRollbackFailed(logger, exception.GetType().Name);
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rolling back a unit of work failed with {ExceptionType}")]
    private static partial void LogRollbackFailed(ILogger logger, string exceptionType);
}
