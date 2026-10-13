using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantSnapshotRetentionRepository(ApplicationDbContext db, ITenantContext tenant)
    : IAssistantSnapshotRetentionRepository
{
    public async Task<IReadOnlyList<AssistantSnapshotRetentionCandidate>> ListExpiredAsync(DateTime nowUtc, CancellationToken ct,
        AssistantSnapshotRetentionCandidate? after = null)
    {
        if (after is { IndexId: var indexId } && indexId == Guid.Empty)
            throw new ArgumentException("A cursor must come from the retention index projection.", nameof(after));
        var query = after is null ? db.Set<AssistantSnapshotRetentionIndex>().AsNoTracking()
            : db.Set<AssistantSnapshotRetentionIndex>().FromSqlInterpolated($"""
                SELECT * FROM platform."AssistantSnapshotRetentionIndices"
                WHERE ("ExpiresAtUtc", "Id") > ({after.ExpiresAtUtc}, {after.IndexId})
                """).AsNoTracking();
        return await query.Where(row => row.ExpiresAtUtc <= nowUtc)
            .OrderBy(row => row.ExpiresAtUtc).ThenBy(row => row.Id).Take(100)
            .Select(row => new AssistantSnapshotRetentionCandidate(row.OperationId, row.ScopeTenantId)
            { IndexId = row.Id, ExpiresAtUtc = row.ExpiresAtUtc }).ToArrayAsync(ct);
    }

    public async Task<AssistantSnapshotCleanupRow> LockExpiredAsync(AssistantSnapshotRetentionCandidate candidate, DateTime nowUtc, CancellationToken ct)
    {
        db.RequireTransaction();
        if (tenant.TenantId != candidate.ScopeTenantId) throw new InvalidOperationException("Retention requires the candidate's tenant scope before the transaction.");
        var indices = await db.Set<AssistantSnapshotRetentionIndex>().FromSqlInterpolated($"""
            SELECT * FROM platform."AssistantSnapshotRetentionIndices"
            WHERE "OperationId" = {candidate.OperationId} AND "ScopeTenantId" IS NOT DISTINCT FROM {candidate.ScopeTenantId}
              AND "ExpiresAtUtc" <= {nowUtc} FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct);
        if (indices.Length == 0) return new(false, null);
        if (candidate.ScopeTenantId is { } tenantId)
        {
            var rows = await db.Set<AssistantDurableOperation>().FromSqlInterpolated($"""
                SELECT *, xmin FROM tenant."AssistantDurableOperations"
                WHERE "Id" = {candidate.OperationId} AND "TenantId" = {tenantId} FOR UPDATE SKIP LOCKED
                """).ToArrayAsync(ct);
            var busy = rows.Length == 0 && await db.Set<AssistantDurableOperation>().AsNoTracking()
                .AnyAsync(row => row.Id == candidate.OperationId && row.TenantId == tenantId, ct);
            return new(!busy, rows.FirstOrDefault());
        }
        else
        {
            var rows = await db.Set<PlatformAssistantDurableOperation>().FromSqlInterpolated($"""
                SELECT *, xmin FROM platform."PlatformAssistantDurableOperations"
                WHERE "Id" = {candidate.OperationId} FOR UPDATE SKIP LOCKED
                """).ToArrayAsync(ct);
            var busy = rows.Length == 0 && await db.Set<PlatformAssistantDurableOperation>().AsNoTracking()
                .AnyAsync(row => row.Id == candidate.OperationId, ct);
            return new(!busy, rows.FirstOrDefault());
        }
    }

    public Task RemoveIndicesAsync(IReadOnlyList<Guid> operationIds, CancellationToken ct) =>
        AssistantSnapshotRetentionWriter.RemoveIndicesAsync(db, operationIds.ToArray(), ct);

    public void RemoveOperation(AssistantDurableOperationBase operation) => db.Remove(operation);

    public async Task<IReadOnlyList<AssistantDurableOperationBase>> LockAccountOperationsAsync(Guid userId, CancellationToken ct)
    {
        db.RequireTransaction();
        if (tenant.TenantId is { } tenantId)
        {
            // The deletion processor already holds the target identity; taking shares on other owners would form cycles.
            var ids = await db.Set<AssistantDurableOperation>().AsNoTracking()
                .Where(row => row.TenantId == tenantId && (row.OwnerUserId == userId || row.SubjectUserIds.Contains(userId)))
                .Select(row => row.Id).ToArrayAsync(ct);
            await AssistantSnapshotRetentionWriter.LockIndicesAsync(db, ids, ct);
            var rows = await db.Set<AssistantDurableOperation>().FromSqlInterpolated($"""
                SELECT *, xmin FROM tenant."AssistantDurableOperations"
                WHERE "TenantId" = {tenantId} AND ("OwnerUserId" = {userId} OR "SubjectUserIds" @> ARRAY[{userId}]::uuid[])
                ORDER BY "Id" FOR UPDATE
                """).ToArrayAsync(ct);
            return rows;
        }
        else
        {
            var ids = await db.Set<PlatformAssistantDurableOperation>().AsNoTracking()
                .Where(row => row.OwnerUserId == userId || row.SubjectUserIds.Contains(userId)).Select(row => row.Id).ToArrayAsync(ct);
            await AssistantSnapshotRetentionWriter.LockIndicesAsync(db, ids, ct);
            var rows = await db.Set<PlatformAssistantDurableOperation>().FromSqlInterpolated($"""
                SELECT *, xmin FROM platform."PlatformAssistantDurableOperations"
                WHERE "OwnerUserId" = {userId} OR "SubjectUserIds" @> ARRAY[{userId}]::uuid[] ORDER BY "Id" FOR UPDATE
                """).ToArrayAsync(ct);
            return rows;
        }
    }
}
