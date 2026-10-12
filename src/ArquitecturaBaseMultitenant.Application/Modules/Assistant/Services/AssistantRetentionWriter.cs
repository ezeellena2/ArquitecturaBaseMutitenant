using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantRetentionWriter(IAssistantDurableRepository durable, IAssistantConversationRepository conversations,
    IPlatformAssistantConversationRepository platform, TimeProvider clock)
{
    internal async Task<int> PurgeAsync(AssistantConversationRetentionRow candidate, DateTime cutoffUtc, CancellationToken ct)
    {
        await durable.LockAsync(candidate.OwnerUserId, candidate.Access, Guid.Empty, candidate.Id, ct);
        IAssistantConversation? row = candidate.Access == Access.Platform
            ? await platform.GetByIdAsync(candidate.OwnerUserId, candidate.Id, null, ct)
            : await conversations.GetByIdAsync(candidate.OwnerUserId, candidate.Access, candidate.Id, null, ct);
        if (row is null || row.IsSaved || row.LastActivityAtUtc > cutoffUtc) return 0;
        await durable.StageFileCleanupAsync(candidate.OwnerUserId, candidate.Access, candidate.Id, clock.GetUtcNow().UtcDateTime, ct);
        await durable.RemoveContentAsync(candidate.OwnerUserId, candidate.Access, candidate.Id, ct);
        if (row is PlatformAssistantConversation global) platform.Remove(global);
        else conversations.Remove((AssistantConversation)row);
        return 1;
    }
}
