using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;

/// <summary>All content mutation paths lock the union of technical indices before touching durable rows.</summary>
internal static class AssistantSnapshotRetentionWriter
{
    internal static async Task LockIndicesAsync(ApplicationDbContext db, Guid[] operationIds, CancellationToken ct)
    {
        db.RequireTransaction();
        if (operationIds.Length == 0) return;
        await db.Database.SqlQuery<Guid>($"""
            SELECT "OperationId" AS "Value" FROM platform."AssistantSnapshotRetentionIndices"
            WHERE "OperationId" = ANY({operationIds}) ORDER BY "OperationId" FOR UPDATE
            """).ToArrayAsync(ct);
    }

    internal static async Task RemoveIndicesAsync(ApplicationDbContext db, Guid[] operationIds, CancellationToken ct)
    {
        db.RequireTransaction();
        if (operationIds.Length == 0) return;
        db.RemoveRange(await db.Set<AssistantSnapshotRetentionIndex>()
            .Where(row => operationIds.Contains(row.OperationId)).ToArrayAsync(ct));
    }
}
