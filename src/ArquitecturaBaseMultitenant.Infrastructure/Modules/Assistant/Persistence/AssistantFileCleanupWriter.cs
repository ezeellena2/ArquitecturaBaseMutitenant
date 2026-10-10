using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;

internal static class AssistantFileCleanupWriter
{
    internal static async Task StageAsync(ApplicationDbContext db, Guid owner, Guid conversationId, bool platform, DateTime nowUtc, CancellationToken ct)
    {
        db.RequireTransaction();
        if (platform)
        {
            var files = await db.Set<PlatformAssistantAttachment>().Where(row => row.ConversationId == conversationId &&
                db.Set<PlatformAssistantConversation>().Any(parent => parent.Id == conversationId && parent.OwnerUserId == owner)).ToArrayAsync(ct);
            foreach (var file in files)
                if (!db.Set<PlatformAssistantFileCleanup>().Local.Any(row => row.Id == file.Id) &&
                    !await db.Set<PlatformAssistantFileCleanup>().AnyAsync(row => row.Id == file.Id, ct))
                    db.Add(PlatformAssistantFileCleanup.Create(file.Id, owner, file.StorageKey, nowUtc));
        }
        else
        {
            var files = await db.Set<AssistantAttachment>().Where(row => row.ConversationId == conversationId &&
                db.Set<AssistantConversation>().Any(parent => parent.Id == conversationId && parent.TenantId == row.TenantId && parent.OwnerUserId == owner)).ToArrayAsync(ct);
            foreach (var file in files)
                if (!db.Set<AssistantFileCleanup>().Local.Any(row => row.Id == file.Id) &&
                    !await db.Set<AssistantFileCleanup>().AnyAsync(row => row.Id == file.Id, ct))
                    db.Add(AssistantFileCleanup.Create(file.Id, owner, file.StorageKey, nowUtc));
        }
    }
}
