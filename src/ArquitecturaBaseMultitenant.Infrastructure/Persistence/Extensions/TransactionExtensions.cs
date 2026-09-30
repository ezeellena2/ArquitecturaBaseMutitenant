using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>Comprueba si una operación técnica se ejecuta dentro o fuera del límite de guardado correcto. Evita que locks, escrituras y lecturas para caché usen una transacción equivocada.</summary>
internal static class TransactionExtensions
{
    /// <summary>Un lock dura lo que la transacción del único límite de guardado.</summary>
    public static void RequireTransaction(this DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "This operation needs the transaction of the use case: call it inside IUnitOfWork.ExecuteInTransactionAsync.");
        }
    }

    /// <summary>Una acción posterior al commit no puede observar cambios aún reversibles.</summary>
    public static void RequireNoTransaction(this DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "This operation runs after the use case commits: call it outside IUnitOfWork.ExecuteInTransactionAsync.");
        }
    }
}
