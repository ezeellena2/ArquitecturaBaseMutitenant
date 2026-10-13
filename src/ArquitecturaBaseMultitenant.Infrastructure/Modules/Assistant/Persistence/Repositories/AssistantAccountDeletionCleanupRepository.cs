using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantAccountDeletionCleanupRepository(ApplicationDbContext db) : IAssistantAccountDeletionCleanupRepository
{
    public void RequireTransaction() => db.RequireTransaction();
    public async Task<IReadOnlyList<AssistantTurnWork>> ListActiveWorksAsync(Guid userId, CancellationToken ct) =>
        await db.Set<AssistantTurnWork>().Where(row => row.OwnerUserId == userId &&
            (row.Status == AssistantTurnStatus.Pending || row.Status == AssistantTurnStatus.Running)).ToArrayAsync(ct);

    public async Task<IReadOnlyList<AssistantTurnWork>> LockWorksAsync(Guid userId, Guid? tenantId, CancellationToken ct)
    {
        db.RequireTransaction();
        return await db.Set<AssistantTurnWork>().FromSqlInterpolated($"""
            SELECT * FROM platform."AssistantTurnWorks"
            WHERE "OwnerUserId" = {userId} AND "ScopeTenantId" IS NOT DISTINCT FROM {tenantId}
            ORDER BY "Id" FOR UPDATE
            """).ToArrayAsync(ct);
    }
    public Task LockFinanceAsync(Guid userId, Guid? tenantId, CancellationToken ct)
    {
        var keys = new List<string> { AssistantLockKeys.AccountFinance(userId) };
        if (tenantId is { } id) keys.Add(AssistantLockKeys.Budget(id));
        return db.AcquireAdvisoryLocksAsync(keys, ct);
    }
    public async Task<IReadOnlyList<AssistantFinancialOperation>> ListFinancialOperationsAsync(Guid userId, Guid? tenantId, CancellationToken ct) =>
        await db.Set<AssistantFinancialOperation>().Where(row => row.UserId == userId && row.ScopeTenantId == tenantId).ToArrayAsync(ct);
    public void RemoveWorks(IEnumerable<AssistantTurnWork> works) => db.RemoveRange(works);

    public async Task RemovePrivateContentAsync(Guid userId, Guid tenantId, DateTime nowUtc, CancellationToken ct)
    {
        db.RequireTransaction();
        var conversations = await db.Set<AssistantConversation>().Where(row => row.OwnerUserId == userId && row.TenantId == tenantId).ToArrayAsync(ct);
        foreach (var row in conversations) await AssistantFileCleanupWriter.StageAsync(db, userId, row.Id, false, nowUtc, ct);
        db.RemoveRange(conversations);
        db.RemoveRange(await db.Set<AssistantUsageFact>().Where(row => row.UserId == userId && row.TenantId == tenantId).ToArrayAsync(ct));
        db.RemoveRange(await db.Set<AssistantOwnKeyDailyQuota>().Where(row => row.UserId == userId && row.TenantId == tenantId).ToArrayAsync(ct));
    }
    public async Task RemoveGlobalContentAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        db.RequireTransaction();
        var conversations = await db.Set<PlatformAssistantConversation>().Where(row => row.OwnerUserId == userId).ToArrayAsync(ct);
        foreach (var row in conversations) await AssistantFileCleanupWriter.StageAsync(db, userId, row.Id, true, nowUtc, ct);
        db.RemoveRange(conversations);
        db.RemoveRange(await db.Set<AssistantNoticeAcceptance>().Where(row => row.OwnerUserId == userId).ToArrayAsync(ct));
        db.RemoveRange(await db.Set<AssistantPlatformKeyDailyQuota>().Where(row => row.UserId == userId).ToArrayAsync(ct));
    }
}
