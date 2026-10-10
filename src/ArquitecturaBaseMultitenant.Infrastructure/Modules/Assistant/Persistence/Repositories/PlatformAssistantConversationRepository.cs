using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;
internal sealed class PlatformAssistantConversationRepository(ApplicationDbContext context) : IPlatformAssistantConversationRepository
{
    public void Add(PlatformAssistantConversation conversation) => context.Set<PlatformAssistantConversation>().Add(conversation);
    public void Add(PlatformAssistantMessage message) => context.Set<PlatformAssistantMessage>().Add(message);
    public void Remove(PlatformAssistantConversation conversation) => context.Remove(conversation);
    public Task<DateTime?> LastMessageAtAsync(Guid ownerUserId, Guid conversationId, CancellationToken ct) =>
        context.Set<PlatformAssistantMessage>().Where(value => value.ConversationId == conversationId
            && context.Set<PlatformAssistantConversation>().Any(parent => parent.Id == conversationId && parent.OwnerUserId == ownerUserId))
            .MaxAsync(value => (DateTime?)value.CreatedAtUtc, ct);
    public void MarkUpdated(PlatformAssistantConversation conversation) => context.Entry(conversation).State = EntityState.Modified;
    public void MarkUpdated(PlatformAssistantMessage message) => context.Entry(message).State = EntityState.Modified;
    public async Task<PlatformAssistantMessage?> GetMessageAsync(Guid ownerUserId, Guid conversationId, Guid messageId, uint? expectedVersion, CancellationToken ct)
    {
        var row = await context.Set<PlatformAssistantMessage>().SingleOrDefaultAsync(value => value.Id == messageId && value.ConversationId == conversationId
            && context.Set<PlatformAssistantConversation>().Any(parent => parent.Id == conversationId && parent.OwnerUserId == ownerUserId), ct);
        if (row is not null && expectedVersion is { } version) context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }
    public async Task<PlatformAssistantConversation?> GetByIdAsync(Guid ownerUserId, Guid conversationId, uint? expectedVersion, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        var row = await context.Set<PlatformAssistantConversation>().SingleOrDefaultAsync(value => value.Id == conversationId && value.OwnerUserId == ownerUserId, ct);
        if (row is not null && expectedVersion is { } version) context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }
}
