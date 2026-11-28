using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>El filtro de partes deja ver solo los vínculos de la parte activa (persona u organización).</summary>
internal sealed class WhatsAppConversationLinkRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppConversationLinkRepository
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

    public async Task<string?> FindChatWaIdAsync(Guid chatId, CancellationToken ct)
    {
        context.RequireTransaction();
        return context.Set<WhatsAppChat>().Local.SingleOrDefault(value => value.Id == chatId)?.WaId
            ?? await context.Set<WhatsAppChat>().AsNoTracking().Where(value => value.Id == chatId)
                .Select(value => value.WaId).SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<WhatsAppConversationLink>> ListForConsumerAsync(Guid consumerTenantId, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<WhatsAppConversationLink>().Where(value => value.ConsumerTenantId == consumerTenantId).ToArrayAsync(ct);
    }

    public async Task RemoveByAccountAndPhoneAsync(Guid consumerTenantId, Guid accountId, string waId, CancellationToken ct)
    {
        context.RequireTransaction();
        var current = tenant.TenantId?.ToString("D") ?? string.Empty;
        var scoped = current;
        async Task ScopeAsync(string value)
        {
            if (scoped == value) return;
            await context.Database.ExecuteSqlAsync($"SELECT set_config('app.tenant_id', {value}, true)", ct);
            scoped = value;
        }

        // Quitar un teléfono puede correr desde Personal, desde una organización o sin espacio: entra al espacio personal y
        // luego a cada organización vinculada bajo la misma transacción (RLS sigue forzado) y restaura app.tenant_id al salir.
        await ScopeAsync(consumerTenantId.ToString("D"));
        var candidates = await context.Database.SqlQuery<LinkCandidate>($"""
            SELECT "Id", "BusinessTenantId", "ChatId"
            FROM engagement."WhatsAppConversationLinks"
            WHERE "ConsumerTenantId" = {consumerTenantId} AND "AccountId" = {accountId}
            """).ToArrayAsync(ct);
        foreach (var group in candidates.GroupBy(row => row.BusinessTenantId).OrderBy(group => group.Key))
        {
            var businessTenantId = group.Key;
            await ScopeAsync(businessTenantId.ToString("D"));
            Guid[] chatIds = [.. group.Select(row => row.ChatId)];
            var matchingChats = (await context.Database.SqlQuery<Guid>($"""
                SELECT "Id" AS "Value"
                FROM tenant."WhatsAppChats"
                WHERE "TenantId" = {businessTenantId} AND "WaId" = {waId} AND "Id" = ANY({chatIds})
                """).ToArrayAsync(ct)).ToHashSet();
            Guid[] linkIds = [.. group.Where(row => matchingChats.Contains(row.ChatId)).Select(row => row.Id)];
            if (linkIds.Length == 0) continue;
            await context.Database.ExecuteSqlAsync($"""
                DELETE FROM engagement."WhatsAppConversationLinks"
                WHERE "ConsumerTenantId" = {consumerTenantId} AND "BusinessTenantId" = {businessTenantId}
                  AND "Id" = ANY({linkIds})
                """, ct);
        }
        await ScopeAsync(current);
    }

    public void Add(WhatsAppConversationLink link) { context.RequireTransaction(); context.Set<WhatsAppConversationLink>().Add(link); }
    public void Add(WhatsAppLinkedMessage message) { context.RequireTransaction(); context.Set<WhatsAppLinkedMessage>().Add(message); }
    public void Remove(WhatsAppConversationLink link) { context.RequireTransaction(); context.Set<WhatsAppConversationLink>().Remove(link); }

#pragma warning disable CA1812 // Proyección de SqlQuery de EF.
    private sealed class LinkCandidate
    {
        public Guid Id { get; init; }
        public Guid BusinessTenantId { get; init; }
        public Guid ChatId { get; init; }
    }
#pragma warning restore CA1812
}
