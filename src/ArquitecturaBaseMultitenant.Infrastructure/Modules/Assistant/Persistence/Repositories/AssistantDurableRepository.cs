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
                new IdempotentReference(row.Kind, row.Id, row.StatusCode, row.Location), row.ConversationId, row.BodyHash, row.Route, row.SnapshotJson, row.QuickKey, row.Version)).SingleOrDefaultAsync(ct)
            : Private(owner, access).Where(row => row.Key == key).Select(row => new AssistantDurableOperationRow(
                new IdempotentReference(row.Kind, row.Id, row.StatusCode, row.Location), row.ConversationId, row.BodyHash, row.Route, row.SnapshotJson, row.QuickKey, row.Version)).SingleOrDefaultAsync(ct);

    public Task<AssistantDurableOperationRow?> GetByIdAsync(Guid owner, Access access, Guid id, CancellationToken ct) =>
        access == Access.Platform
            ? Platform(owner).Where(row => row.Id == id).Select(row => new AssistantDurableOperationRow(
                new IdempotentReference(row.Kind, row.Id, row.StatusCode, row.Location), row.ConversationId, row.BodyHash, row.Route, row.SnapshotJson, row.QuickKey, row.Version)).SingleOrDefaultAsync(ct)
            : Private(owner, access).Where(row => row.Id == id).Select(row => new AssistantDurableOperationRow(
                new IdempotentReference(row.Kind, row.Id, row.StatusCode, row.Location), row.ConversationId, row.BodyHash, row.Route, row.SnapshotJson, row.QuickKey, row.Version)).SingleOrDefaultAsync(ct);

    private IQueryable<PlatformAssistantDurableOperation> Platform(Guid owner) =>
        db.Set<PlatformAssistantDurableOperation>().AsNoTracking().Where(row => row.OwnerUserId == owner);
    private IQueryable<AssistantDurableOperation> Private(Guid owner, Access access) =>
        db.Set<AssistantDurableOperation>().AsNoTracking().Where(row => row.OwnerUserId == owner && row.Access == access && row.TenantId == tenant.RequiredTenantId);

    public Entity Add(Guid owner, Access access, IdempotentRequest request, Guid conversationId,
        IdempotentReference reference, string snapshot, string? quickKey, DateTime nowUtc)
    {
        Entity row = access == Access.Platform
            ? PlatformAssistantDurableOperation.Create(owner, request.Key, request.BodyHash, request.Route, conversationId,
                reference.Kind, reference.StatusCode, reference.Location, snapshot, quickKey, nowUtc)
            : AssistantDurableOperation.Create(owner, access, request.Key, request.BodyHash, request.Route, conversationId,
                reference.Kind, reference.StatusCode, reference.Location, snapshot, quickKey, nowUtc);
        db.Add(row);
        return row;
    }

    public async Task RemoveContentAsync(Guid owner, Access access, Guid conversationId, CancellationToken ct)
    {
        if (access == Access.Platform)
        {
            var rows = await db.Set<PlatformAssistantDurableOperation>().Where(row => row.OwnerUserId == owner && row.ConversationId == conversationId).ToArrayAsync(ct);
            foreach (var row in rows) row.RemoveContent();
        }
        else
        {
            var rows = await db.Set<AssistantDurableOperation>().Where(row => row.OwnerUserId == owner && row.Access == access && row.TenantId == tenant.RequiredTenantId && row.ConversationId == conversationId).ToArrayAsync(ct);
            foreach (var row in rows) row.RemoveContent();
        }
    }
}
