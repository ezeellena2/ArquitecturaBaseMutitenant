using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>
/// Los contadores de cuotas en PostgreSQL (E9), con SQL de una sola sentencia por cuota. Toma los locks en el orden de las
/// claves, así dos pedidos con cuotas en común nunca se bloquean entre sí.
/// </summary>
internal sealed class RateLimitCounterRepository(ApplicationDbContext context) : IRateLimitCounterRepository
{
    public async Task<IReadOnlyList<int>> LockAsync(IReadOnlyList<RateLimitBucket> buckets, IReadOnlyList<int> floors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        ArgumentNullException.ThrowIfNull(floors);
        context.RequireTransaction();
        var counts = new int[buckets.Count];
        foreach (var index in Enumerable.Range(0, buckets.Count).OrderBy(index => buckets[index].Key, StringComparer.Ordinal))
        {
            var bucket = buckets[index];
            // Crea la fila o la toma con lock; nunca la deja por debajo de lo que esta instancia vio en Redis. Un INSERT no
            // se compone: la fila que devuelve se toma del lado del cliente.
            var returned = await context.Database.SqlQuery<int>($"""
                INSERT INTO platform."RateLimitCounters" ("Key", "Count", "ExpiresAtUtc")
                VALUES ({bucket.Key}, {Math.Max(floors[index], 0)}, {bucket.Window.EndUtc})
                ON CONFLICT ("Key") DO UPDATE
                    SET "Count" = GREATEST(platform."RateLimitCounters"."Count", EXCLUDED."Count")
                RETURNING "Count" AS "Value"
                """).ToListAsync(cancellationToken);
            counts[index] = returned.Single();
        }

        return counts;
    }

    public Task IncrementAsync(IReadOnlyList<RateLimitBucket> buckets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        context.RequireTransaction();
        string[] keys = [.. buckets.Select(bucket => bucket.Key)];
        return context.Database.ExecuteSqlAsync($"""
            UPDATE platform."RateLimitCounters" SET "Count" = "Count" + 1 WHERE "Key" = ANY({keys})
            """, cancellationToken);
    }

    public Task<int> DeleteExpiredAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.Database.ExecuteSqlAsync($"""
            DELETE FROM platform."RateLimitCounters" WHERE "Key" IN (
                SELECT "Key" FROM platform."RateLimitCounters" WHERE "ExpiresAtUtc" <= {nowUtc}
                ORDER BY "ExpiresAtUtc" LIMIT {batchSize} FOR UPDATE SKIP LOCKED)
            """, cancellationToken);
    }
}
