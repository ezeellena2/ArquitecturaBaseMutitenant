using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Retention;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADR 0062: borra por condición, en lotes y en la transacción del llamador, los códigos y los ingresos vencidos de plazo.
/// Sin cargar las filas ni pasar por el seguimiento de cambios. Dos copias que corren a la vez borran a lo sumo lo mismo:
/// un borrado de una fila que ya no está no hace nada.
/// </summary>
internal sealed class IdentityRetentionRepository(ApplicationDbContext context) : IIdentityRetentionRepository
{
    /// <summary>
    /// Con <c>FOR UPDATE SKIP LOCKED</c>: dos copias se reparten los códigos en vez de esperarse. Nada más toca un código
    /// de hace un mes.
    /// </summary>
    public Task<int> DeleteLoginCodesAsync(DateTime createdBeforeUtc, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        context.RequireTransaction();
        return context.Database.ExecuteSqlAsync($"""
            DELETE FROM identity."LoginCodes" WHERE "Id" IN (
                SELECT "Id" FROM identity."LoginCodes" WHERE "CreatedAtUtc" < {createdBeforeUtc}
                ORDER BY "CreatedAtUtc", "Id" LIMIT {limit} FOR UPDATE SKIP LOCKED)
            """, cancellationToken);
    }

    /// <summary>
    /// Mira la página por fecha y la borra salvo el último ingreso exitoso de cada cuenta, que muestra «Último ingreso». Sin
    /// <c>FOR UPDATE</c>: mt_app no edita los ingresos, solo los suma y los borra. El último se decide sobre la tabla entera,
    /// así que un ingreso nuevo de la cuenta deja borrar el anterior en la corrida siguiente, y el último nunca se borra.
    /// </summary>
    public async Task<RetentionBatch> DeleteLoginAuditsAsync(DateTime occurredBeforeUtc, RetentionCursor? after,
        int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        context.RequireTransaction();
        var (afterAtUtc, afterId) = Start(after);
        var page = await context.Database.SqlQuery<RetentionKeyRow>($"""
            SELECT "Id", "OccurredAtUtc" AS "AtUtc" FROM identity."LoginAudits"
            WHERE "OccurredAtUtc" < {occurredBeforeUtc} AND ("OccurredAtUtc", "Id") > ({afterAtUtc}, {afterId})
            ORDER BY "OccurredAtUtc", "Id" LIMIT {limit}
            """).ToArrayAsync(cancellationToken);
        if (page.Length == 0) return new RetentionBatch(0, null);

        Guid[] ids = [.. page.Select(row => row.Id)];
        var removed = await context.Database.ExecuteSqlAsync($"""
            DELETE FROM identity."LoginAudits" audit
            WHERE audit."Id" = ANY({ids})
              AND NOT (audit."Succeeded" AND audit."UserId" IS NOT NULL AND NOT EXISTS (
                  SELECT 1 FROM identity."LoginAudits" newer
                  WHERE newer."UserId" = audit."UserId" AND newer."Succeeded"
                    AND (newer."OccurredAtUtc", newer."Id") > (audit."OccurredAtUtc", audit."Id")))
            """, cancellationToken);
        return new RetentionBatch(removed, Next(page, limit));
    }

    internal static (DateTime AtUtc, Guid Id) Start(RetentionCursor? after) =>
        after is { } cursor ? (cursor.AtUtc, cursor.Id) : (DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), Guid.Empty);

    /// <summary>Una página llena puede tener más detrás: el próximo lote sigue después de su última fila.</summary>
    internal static RetentionCursor? Next(RetentionKeyRow[] page, int limit) =>
        page.Length < limit ? null : new RetentionCursor(page[^1].AtUtc, page[^1].Id);
}

/// <summary>Una fila que miró un lote de la retención: su Id y la fecha por la que se recorre la tabla.</summary>
internal sealed class RetentionKeyRow
{
    public Guid Id { get; init; }

    public DateTime AtUtc { get; init; }
}
