using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantDurableRepository(ApplicationDbContext db, ITenantContext tenant) : IAssistantDurableRepository
{
    public Task StageFileCleanupAsync(Guid owner, Access access, Guid conversationId, DateTime nowUtc, CancellationToken ct) =>
        AssistantFileCleanupWriter.StageAsync(db, owner, conversationId, access == Access.Platform, nowUtc, ct);
    public Task LockAsync(Guid owner, Access access, Guid key, Guid? conversationId, CancellationToken ct)
    {
        var keys = new List<string>();
        if (key != Guid.Empty) keys.Add(LockKey(owner, access, "binding", key));
        if (conversationId is { } id) keys.Add(LockKey(owner, access, "conversation", id));
        return db.AcquireAdvisoryLocksAsync(keys, ct);
    }

    private string LockKey(Guid owner, Access access, string resource, Guid id) => access == Access.Platform
        ? AdvisoryLockKeys.Account(owner) + ":assistant:" + resource + ":" + id.ToString("N")
        : AdvisoryLockKeys.For(tenant.RequiredTenantId, "assistant:" + resource + ":" + owner.ToString("N") + ":" + access, id);

    public Task<AssistantDurableOperationRow?> GetByKeyAsync(Guid owner, Access access, Guid key, CancellationToken ct) =>
        access == Access.Platform
            ? Platform(owner).Where(row => row.Key == key).Select(row => new AssistantDurableOperationRow(
                new IdempotentReference(row.Kind, row.Id, row.StatusCode, row.Location), row.ConversationId, row.BodyHash, row.Route, row.SnapshotJson, row.QuickKey, EffectiveVersion(row.OriginalSnapshotVersion, row.Version), row.CreatedAtUtc) { SubjectUserIds = row.SubjectUserIds }).SingleOrDefaultAsync(ct)
            : Private(owner, access).Where(row => row.Key == key).Select(row => new AssistantDurableOperationRow(
                new IdempotentReference(row.Kind, row.Id, row.StatusCode, row.Location), row.ConversationId, row.BodyHash, row.Route, row.SnapshotJson, row.QuickKey, EffectiveVersion(row.OriginalSnapshotVersion, row.Version), row.CreatedAtUtc) { SubjectUserIds = row.SubjectUserIds }).SingleOrDefaultAsync(ct);

    public Task<AssistantDurableOperationRow?> GetByIdAsync(Guid owner, Access access, Guid id, CancellationToken ct) =>
        access == Access.Platform
            ? Platform(owner).Where(row => row.Id == id).Select(row => new AssistantDurableOperationRow(
                new IdempotentReference(row.Kind, row.Id, row.StatusCode, row.Location), row.ConversationId, row.BodyHash, row.Route, row.SnapshotJson, row.QuickKey, EffectiveVersion(row.OriginalSnapshotVersion, row.Version), row.CreatedAtUtc) { SubjectUserIds = row.SubjectUserIds }).SingleOrDefaultAsync(ct)
            : Private(owner, access).Where(row => row.Id == id).Select(row => new AssistantDurableOperationRow(
                new IdempotentReference(row.Kind, row.Id, row.StatusCode, row.Location), row.ConversationId, row.BodyHash, row.Route, row.SnapshotJson, row.QuickKey, EffectiveVersion(row.OriginalSnapshotVersion, row.Version), row.CreatedAtUtc) { SubjectUserIds = row.SubjectUserIds }).SingleOrDefaultAsync(ct);

    // Project both physical columns; bigint and PostgreSQL xid cannot safely share a SQL COALESCE.
    private static uint EffectiveVersion(uint? originalSnapshotVersion, uint version) => originalSnapshotVersion ?? version;

    private IQueryable<PlatformAssistantDurableOperation> Platform(Guid owner) =>
        db.Set<PlatformAssistantDurableOperation>().AsNoTracking().Where(row => row.OwnerUserId == owner);
    private IQueryable<AssistantDurableOperation> Private(Guid owner, Access access) =>
        db.Set<AssistantDurableOperation>().AsNoTracking().Where(row => row.OwnerUserId == owner && row.Access == access && row.TenantId == tenant.RequiredTenantId);

    public Entity Add(Guid owner, Access access, IdempotentRequest request, Guid conversationId,
        IdempotentReference reference, string snapshot, string? quickKey, DateTime nowUtc, Guid[]? subjectUserIds = null)
    {
        db.RequireTransaction();
        Entity row = access == Access.Platform
            ? PlatformAssistantDurableOperation.Create(owner, request.Key, request.BodyHash, request.Route, conversationId,
                reference.Kind, reference.StatusCode, reference.Location, snapshot, quickKey, nowUtc, subjectUserIds)
            : AssistantDurableOperation.Create(owner, access, request.Key, request.BodyHash, request.Route, conversationId,
                reference.Kind, reference.StatusCode, reference.Location, snapshot, quickKey, nowUtc, subjectUserIds);
        db.Add(row);
        db.Add(AssistantSnapshotRetentionIndex.Create(row.Id, access == Access.Platform ? null : tenant.RequiredTenantId, nowUtc));
        return row;
    }

    public async Task RemoveContentAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct)
    {
        db.RequireTransaction();
        if (access == Access.Platform)
        {
            var ids = await Platform(owner).Where(row => row.ConversationId == conversationId).Select(row => row.Id).ToArrayAsync(ct);
            await AssistantSnapshotRetentionWriter.LockIndicesAsync(db, ids, ct);
            var rows = await db.Set<PlatformAssistantDurableOperation>().FromSqlInterpolated($"""
                SELECT *, xmin FROM platform."PlatformAssistantDurableOperations"
                WHERE "OwnerUserId" = {owner} AND "ConversationId" = {conversationId} ORDER BY "Id" FOR UPDATE
                """).ToArrayAsync(ct);
            foreach (var row in rows) row.RemoveContent();
            await AssistantSnapshotRetentionWriter.RemoveIndicesAsync(db, ids, ct);
        }
        else
        {
            var ids = await Private(owner, access).Where(row => row.ConversationId == conversationId).Select(row => row.Id).ToArrayAsync(ct);
            await AssistantSnapshotRetentionWriter.LockIndicesAsync(db, ids, ct);
            var accessValue = access.ToString();
            var rows = await db.Set<AssistantDurableOperation>().FromSqlInterpolated($"""
                SELECT *, xmin FROM tenant."AssistantDurableOperations"
                WHERE "OwnerUserId" = {owner} AND "Access" = {accessValue} AND "TenantId" = {tenant.RequiredTenantId}
                  AND "ConversationId" = {conversationId} ORDER BY "Id" FOR UPDATE
                """).ToArrayAsync(ct);
            foreach (var row in rows) row.RemoveContent();
            await AssistantSnapshotRetentionWriter.RemoveIndicesAsync(db, ids, ct);
        }
    }
}
