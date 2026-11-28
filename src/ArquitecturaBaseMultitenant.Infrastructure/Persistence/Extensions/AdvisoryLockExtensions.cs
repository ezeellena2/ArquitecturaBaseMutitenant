using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>Adquiere locks consultivos de PostgreSQL dentro de la transacción del caso de uso. Ordena las claves para que operaciones concurrentes tomen los locks en el mismo orden.</summary>
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

    /// <summary>
    /// Lo mismo para una lista que crece con los datos (las personas de un rol, por ejemplo): todas las claves en una sola ida a
    /// la base, en el mismo orden. La función volátil se evalúa fila por fila después del ORDER BY, así que cada lock se toma en
    /// el orden del arreglo, como los toma <see cref="AcquireAdvisoryLocksAsync"/>.
    /// </summary>
    public static async Task AcquireAdvisoryLocksInOneCallAsync(
        this DbContext dbContext,
        IEnumerable<string> keys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        dbContext.RequireTransaction();
        string[] ordered = [.. keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (ordered.Length == 0) return;
        await dbContext.Database.ExecuteSqlAsync($"""
            SELECT pg_advisory_xact_lock(hashtextextended(key, 0))
            FROM unnest({ordered}) WITH ORDINALITY AS locks(key, position) ORDER BY position
            """, cancellationToken);
    }
}
