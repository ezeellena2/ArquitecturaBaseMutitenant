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
    public async Task<AssistantConversation?> GetByIdAsync(Guid ownerUserId, Access access, Guid conversationId, uint? expectedVersion, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        var row = await context.Set<AssistantConversation>().SingleOrDefaultAsync(value => value.Id == conversationId && value.OwnerUserId == ownerUserId && value.TenantId == tenant.RequiredTenantId && value.Access == access, ct);
        if (row is not null && expectedVersion is { } version) context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }
}
