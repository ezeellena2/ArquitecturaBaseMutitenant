using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Único límite transaccional y de guardado de un caso de uso que escribe.</summary>
public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CommitPolicy policy,
        CancellationToken cancellationToken)
        where TResult : Result;
}
