using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;
internal sealed class PlatformAssistantConversationRepository(ApplicationDbContext context) : IPlatformAssistantConversationRepository
{
    public void Add(PlatformAssistantConversation conversation) => context.Set<PlatformAssistantConversation>().Add(conversation);
    public async Task<PlatformAssistantConversation?> GetByIdAsync(Guid ownerUserId, Guid conversationId, uint? expectedVersion, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        var row = await context.Set<PlatformAssistantConversation>().SingleOrDefaultAsync(value => value.Id == conversationId && value.OwnerUserId == ownerUserId, ct);
        if (row is not null && expectedVersion is { } version) context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }
}
