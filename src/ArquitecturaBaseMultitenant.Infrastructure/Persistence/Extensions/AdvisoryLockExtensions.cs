using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

internal static class AdvisoryLockExtensions
{
    /// <summary>Toma locks de transacción ordenados y sin duplicar; el texto viaja parametrizado a PostgreSQL.</summary>
    public static async Task AcquireAdvisoryLocksAsync(
        this DbContext dbContext,
        IEnumerable<string> keys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);

        // El caso sin claves también exige el límite: no debe encubrir un llamador fuera del UoW.
        dbContext.RequireTransaction();

        foreach (var key in keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            await dbContext.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        }
    }
}
