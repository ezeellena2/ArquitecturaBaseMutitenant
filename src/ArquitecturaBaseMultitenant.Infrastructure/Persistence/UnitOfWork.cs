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

            if (UniqueViolations.Translate(exception) is { } unique)
            {
                throw unique;
            }

            throw;
        }
        finally
        {
            _transaction = null;
            await transaction.DisposeAsync();
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
