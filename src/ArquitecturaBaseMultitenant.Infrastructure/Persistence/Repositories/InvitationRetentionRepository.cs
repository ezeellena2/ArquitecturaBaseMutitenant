using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Retention;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADR 0062: borra por condición, en lotes y en la transacción del llamador, las invitaciones cerradas de la organización
/// activa. Corre en su alcance, con RLS, y además filtra por la organización. Sin cargar las filas: no deja una entrada de
/// auditoría por cada una. Los triggers de la tabla retiran su proyección de ingreso y el Dueño pendiente, que ya no las
/// tenían por estar cerradas.
/// </summary>
internal sealed class InvitationRetentionRepository(ApplicationDbContext context, ITenantContext tenantContext)
    : IInvitationRetentionRepository
{
    /// <summary>
    /// Recorre las cerradas por la fecha en que se mandaron, que nunca es posterior a la de cierre, con el índice parcial
    /// sobre las cerradas, y toma la página con <c>FOR UPDATE SKIP LOCKED</c>: dos copias se la reparten. Borra la que se
    /// cerró antes del plazo y tiene otra más nueva de la misma persona al mismo destino: así queda la última de cada
    /// destino y la última de cada persona, que nombra a quien no tiene nombre en la auditoría. Una revocada que ya estaba
    /// vencida espera a que su vencimiento quede en la auditoría (<c>InvitationExpiryWorker</c>).
    /// </summary>
    public async Task<RetentionBatch> DeleteClosedAsync(DateTime closedBeforeUtc, RetentionCursor? after, int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        context.RequireTransaction();
        var tenantId = tenantContext.RequiredTenantId;
        var (afterAtUtc, afterId) = IdentityRetentionRepository.Start(after);
        var page = await context.Database.SqlQuery<RetentionKeyRow>($"""
            SELECT "Id", "IssuedAtUtc" AS "AtUtc" FROM tenant."Invitations"
            WHERE "TenantId" = {tenantId} AND "Status" <> 'Pending' AND "IssuedAtUtc" < {closedBeforeUtc}
              AND ("IssuedAtUtc", "Id") > ({afterAtUtc}, {afterId})
            ORDER BY "IssuedAtUtc", "Id" LIMIT {limit} FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(cancellationToken);
        if (page.Length == 0) return new RetentionBatch(0, null);

        Guid[] ids = [.. page.Select(row => row.Id)];
        var removed = await context.Database.ExecuteSqlAsync($"""
            DELETE FROM tenant."Invitations" closed
            WHERE closed."TenantId" = {tenantId} AND closed."Id" = ANY({ids}) AND closed."Status" <> 'Pending'
              AND LEAST(COALESCE(closed."AcceptedAtUtc", closed."RevokedAtUtc", closed."ExpiresAtUtc"),
                  closed."ExpiresAtUtc") < {closedBeforeUtc}
              AND NOT (closed."Status" = 'Revoked' AND closed."ExpiryRecordedAtUtc" IS NULL
                  AND closed."RevokedAtUtc" IS NOT NULL AND closed."RevokedAtUtc" >= closed."ExpiresAtUtc")
              AND EXISTS (
                  SELECT 1 FROM tenant."Invitations" newer
                  WHERE newer."TenantId" = closed."TenantId" AND newer."MemberId" = closed."MemberId"
                    AND newer."Destination" = closed."Destination"
                    AND (newer."IssuedAtUtc", newer."Id") > (closed."IssuedAtUtc", closed."Id"))
            """, cancellationToken);
        return new RetentionBatch(removed, IdentityRetentionRepository.Next(page, limit));
    }
}
