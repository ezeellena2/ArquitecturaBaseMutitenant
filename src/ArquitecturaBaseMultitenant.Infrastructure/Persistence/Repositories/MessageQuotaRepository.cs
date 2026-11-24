using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>
/// Los contadores por hora de los topes de mensajes (ADR 0063), en la transacción del llamador. El lock es un advisory lock
/// por contador, que se suelta con el commit; sumar es un upsert de la hora, que se deshace si la transacción no se guarda.
/// Sin pasar por el seguimiento de cambios: la fila no se carga.
/// </summary>
internal sealed class MessageQuotaRepository(ApplicationDbContext context) : IMessageQuotaRepository
{
    public Task LockAsync(IReadOnlyCollection<MessageQuotaKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return context.AcquireAdvisoryLocksAsync(keys.Select(key => "messaging:quota:" + key.Scope + ":" + key.KeyHash),
            cancellationToken);
    }

    public async Task<IReadOnlyList<MessageQuotaHour>> ListAsync(MessageQuotaKey key, DateTime fromHourUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        context.RequireTransaction();
        var rows = await context.MessageQuotaBuckets.AsNoTracking()
            .Where(bucket => bucket.Scope == key.Scope && bucket.KeyHash == key.KeyHash && bucket.HourUtc >= fromHourUtc)
            .Select(bucket => new { bucket.HourUtc, bucket.Count })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => new MessageQuotaHour(row.HourUtc, row.Count))];
    }

    public Task IncrementAsync(MessageQuotaKey key, DateTime hourUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (hourUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The hour must be UTC.", nameof(hourUtc));
        context.RequireTransaction();
        var scope = key.Scope.ToString();
        return context.Database.ExecuteSqlAsync($"""
            INSERT INTO identity."MessageQuotaBuckets" ("Scope", "KeyHash", "HourUtc", "Count")
            VALUES ({scope}, {key.KeyHash}, {hourUtc}, 1)
            ON CONFLICT ("Scope", "KeyHash", "HourUtc") DO UPDATE SET "Count" = identity."MessageQuotaBuckets"."Count" + 1
            """, cancellationToken);
    }
}
