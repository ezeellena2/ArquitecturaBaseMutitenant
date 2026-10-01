using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;

/// <summary>
/// Simula el límite transaccional para pruebas unitarias sin base de datos. Cuenta commits y rollbacks,
/// aplica CommitPolicy y permite provocar un fallo de commit.
/// </summary>
internal sealed class FakeUnitOfWork(List<string>? events = null) : IUnitOfWork
{
    /// <summary>Cuántas veces se abrió el límite transaccional.</summary>
    public int Transactions { get; private set; }

    /// <summary>Cuántas veces se llegó al commit, incluido uno que falle.</summary>
    public int Commits { get; private set; }

    /// <summary>Cuántas veces se deshizo por Result, excepción o commit fallido.</summary>
    public int Rollbacks { get; private set; }

    public CommitPolicy? LastPolicy { get; private set; }

    public bool InTransaction { get; private set; }

    public Action? OnCommit { get; init; }

    public Exception? CommitFailure { get; set; }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work, CommitPolicy policy, CancellationToken cancellationToken)
        where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(work);
        policy.ThrowIfUndefined();

        if (InTransaction)
        {
            throw new InvalidOperationException("ExecuteInTransactionAsync does not nest.");
        }

        Transactions++;
        LastPolicy = policy;
        InTransaction = true;

        try
        {
            TResult result;
            try
            {
                result = await work(cancellationToken);
            }
            catch
            {
                Rollbacks++;
                throw;
            }

            if (!policy.Commits(result))
            {
                Rollbacks++;
                return result;
            }

            Commits++;
            events?.Add("commit");
            OnCommit?.Invoke();
            if (CommitFailure is { } failure)
            {
                Rollbacks++;
                throw failure;
            }

            return result;
        }
        finally
        {
            InTransaction = false;
        }
    }
}
