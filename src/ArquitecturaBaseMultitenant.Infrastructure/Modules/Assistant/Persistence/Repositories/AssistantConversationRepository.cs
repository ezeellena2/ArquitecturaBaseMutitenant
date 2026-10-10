using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;
internal sealed class AssistantConversationRepository(ApplicationDbContext context, ITenantContext tenant) : IAssistantConversationRepository
{
    public void Add(AssistantConversation conversation) => context.Set<AssistantConversation>().Add(conversation);
    public void Add(AssistantMessage message) => context.Set<AssistantMessage>().Add(message);
    public void Remove(AssistantConversation conversation) => context.Remove(conversation);
    public Task<DateTime?> LastMessageAtAsync(Guid ownerUserId, Access access, Guid conversationId, CancellationToken ct) =>
        context.Set<AssistantMessage>().Where(value => value.ConversationId == conversationId && value.TenantId == tenant.RequiredTenantId
            && context.Set<AssistantConversation>().Any(parent => parent.Id == conversationId && parent.TenantId == value.TenantId && parent.OwnerUserId == ownerUserId && parent.Access == access))
            .MaxAsync(value => (DateTime?)value.CreatedAtUtc, ct);
    public void MarkUpdated(AssistantConversation conversation) => context.Entry(conversation).State = EntityState.Modified;
    public void MarkUpdated(AssistantMessage message) => context.Entry(message).State = EntityState.Modified;
    public async Task<AssistantMessage?> GetMessageAsync(Guid ownerUserId, Access access, Guid conversationId, Guid messageId, uint? expectedVersion, CancellationToken ct)
    {
        var row = await context.Set<AssistantMessage>().SingleOrDefaultAsync(value => value.Id == messageId && value.ConversationId == conversationId && value.TenantId == tenant.RequiredTenantId
            && context.Set<AssistantConversation>().Any(parent => parent.Id == conversationId && parent.TenantId == value.TenantId && parent.OwnerUserId == ownerUserId && parent.Access == access), ct);
        if (row is not null && expectedVersion is { } version) context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }
    public async Task<AssistantConversation?> GetByIdAsync(Guid ownerUserId, Access access, Guid conversationId, uint? expectedVersion, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        var row = await context.Set<AssistantConversation>().SingleOrDefaultAsync(value => value.Id == conversationId && value.OwnerUserId == ownerUserId && value.TenantId == tenant.RequiredTenantId && value.Access == access, ct);
        if (row is not null && expectedVersion is { } version) context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }
}
