using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantRetentionRepository(ApplicationDbContext db, ITenantContext tenant) : IAssistantRetentionRepository
{
    public async Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken ct) => await db.Tenants.AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).ToArrayAsync(ct);
    public async Task<int> RetentionDaysAsync(CancellationToken ct) =>
        await db.Set<AssistantPlatformSettings>().AsNoTracking().Select(row => (int?)row.RetentionDays).SingleOrDefaultAsync(ct) ?? AssistantFeature.DefaultRetentionDays;

    public async Task<IReadOnlyList<AssistantConversationRetentionRow>> ListExpiredAsync(DateTime cutoffUtc, Guid? after, CancellationToken ct)
    {
        if (tenant.TenantId is { } tenantId)
            return await db.Set<AssistantConversation>().AsNoTracking().Where(row => row.TenantId == tenantId && !row.IsSaved && row.LastActivityAtUtc <= cutoffUtc &&
                (after == null || row.Id.CompareTo(after.Value) > 0)).OrderBy(row => row.Id).Take(100)
                .Select(row => new AssistantConversationRetentionRow(row.Id, row.OwnerUserId, row.Access)).ToArrayAsync(ct);
        return await db.Set<PlatformAssistantConversation>().AsNoTracking().Where(row => !row.IsSaved && row.LastActivityAtUtc <= cutoffUtc &&
            (after == null || row.Id.CompareTo(after.Value) > 0)).OrderBy(row => row.Id).Take(100)
            .Select(row => new AssistantConversationRetentionRow(row.Id, row.OwnerUserId, Access.Platform)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<AssistantFileCleanupRow>> ListCleanupAsync(CancellationToken ct) => tenant.TenantId is { } tenantId
        ? await db.Set<AssistantFileCleanup>().AsNoTracking().Where(row => row.TenantId == tenantId).OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id).Take(100)
            .Select(row => new AssistantFileCleanupRow(row.Id, row.StorageKey)).ToArrayAsync(ct)
        : await db.Set<PlatformAssistantFileCleanup>().AsNoTracking().OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id).Take(100)
            .Select(row => new AssistantFileCleanupRow(row.Id, row.StorageKey)).ToArrayAsync(ct);

    public async Task RemoveCleanupAsync(Guid id, CancellationToken ct)
    {
        db.RequireTransaction();
        if (tenant.TenantId is { } tenantId)
        {
            var row = await db.Set<AssistantFileCleanup>().SingleOrDefaultAsync(value => value.Id == id && value.TenantId == tenantId, ct);
            if (row is not null) db.Remove(row);
        }
        else
        {
            var row = await db.Set<PlatformAssistantFileCleanup>().SingleOrDefaultAsync(value => value.Id == id, ct);
            if (row is not null) db.Remove(row);
        }
    }

    public Task<bool> IsStorageReferencedAsync(string storageKey, CancellationToken ct) => tenant.TenantId is { } tenantId
        ? db.Set<AssistantAttachment>().AsNoTracking().AnyAsync(row => row.TenantId == tenantId && row.StorageKey == storageKey, ct)
        : db.Set<PlatformAssistantAttachment>().AsNoTracking().AnyAsync(row => row.StorageKey == storageKey, ct);
}
