using System.Data.Common;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Corre lo que dejó <see cref="IAfterCommit"/> recién cuando se confirma la transacción del scope, como
/// <c>CacheGenerationInterceptor</c> con la caché: un rollback o una falla lo descartan, y una transacción nueva empieza sin
/// pendientes de otra que no terminó.
/// </summary>
internal sealed class AfterCommitInterceptor : DbTransactionInterceptor, IAfterCommit
{
    private readonly List<Action> _pending = [];

    public void Add(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _pending.Add(action);
    }

    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData,
        DbTransaction result)
    {
        _pending.Clear();
        return result;
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection,
        TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return ValueTask.FromResult(result);
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Run();

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Run();
        return Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        _pending.Clear();

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return Task.CompletedTask;
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) =>
        _pending.Clear();

    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return Task.CompletedTask;
    }

    private void Run()
    {
        Action[] actions = [.. _pending];
        _pending.Clear();
        foreach (var action in actions) action();
    }
}
