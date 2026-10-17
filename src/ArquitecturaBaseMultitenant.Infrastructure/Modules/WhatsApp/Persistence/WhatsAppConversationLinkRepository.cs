using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>El filtro de partes deja ver solo los vínculos de la parte activa (persona u organización).</summary>
internal sealed class WhatsAppConversationLinkRepository(ApplicationDbContext context) : IWhatsAppConversationLinkRepository
{
    public async Task<WhatsAppConversationLink?> GetByChatAsync(Guid chatId, CancellationToken ct)
    {
        context.RequireTransaction();
        // Un vínculo recién creado copia ya el mensaje que lo confirma, antes de guardar.
        return context.Set<WhatsAppConversationLink>().Local.SingleOrDefault(value => value.ChatId == chatId)
            ?? await context.Set<WhatsAppConversationLink>().SingleOrDefaultAsync(value => value.ChatId == chatId, ct);
    }

    public Task<WhatsAppConversationLink?> GetAsync(Guid id, CancellationToken ct)
    {
        context.RequireTransaction();
        return context.Set<WhatsAppConversationLink>().SingleOrDefaultAsync(value => value.Id == id, ct);
    }

    public async Task<IReadOnlyList<WhatsAppConversationLink>> ListForConsumerAsync(Guid consumerTenantId, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<WhatsAppConversationLink>().Where(value => value.ConsumerTenantId == consumerTenantId).ToArrayAsync(ct);
    }

    public void Add(WhatsAppConversationLink link) { context.RequireTransaction(); context.Set<WhatsAppConversationLink>().Add(link); }
    public void Add(WhatsAppLinkedMessage message) { context.RequireTransaction(); context.Set<WhatsAppLinkedMessage>().Add(message); }
    public void Remove(WhatsAppConversationLink link) { context.RequireTransaction(); context.Set<WhatsAppConversationLink>().Remove(link); }
}
