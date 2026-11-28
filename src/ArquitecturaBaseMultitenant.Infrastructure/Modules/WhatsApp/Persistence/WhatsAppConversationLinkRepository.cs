using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>El filtro de partes deja ver solo los vínculos de la parte activa (persona u organización).</summary>
internal sealed class WhatsAppConversationLinkRepository(ApplicationDbContext context, ITenantContext tenant, ICurrentUser currentUser,
    TimeProvider clock) : IWhatsAppConversationLinkRepository
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
        var removed = new List<RemovedLink>();
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
            var deleted = await context.Database.SqlQuery<RemovedLink>($"""
                DELETE FROM engagement."WhatsAppConversationLinks"
                WHERE "ConsumerTenantId" = {consumerTenantId} AND "BusinessTenantId" = {businessTenantId}
                  AND "Id" = ANY({linkIds})
                RETURNING "Id", "ConsumerTenantId", "BusinessTenantId", "ChatId", "AccountId", "OrganizationName", "DisplayNumber",
                    "LinkedAtUtc"
                """).ToArrayAsync(ct);
            await AuditAsync(businessTenantId, deleted, ct);
            removed.AddRange(deleted);
        }
        if (removed.Count > 0)
        {
            await ScopeAsync(consumerTenantId.ToString("D"));
            await AuditAsync(consumerTenantId, removed, ct);
        }
        await ScopeAsync(current);
    }

    // Un borrado por SQL no pasa por AuditTrailInterceptor: cada parte recibe la misma entrada que dejaría el interceptor al
    // borrar el vínculo (Deleted, con lo auditado del vínculo como «old»), dentro de su alcance y en la misma transacción.
    private async Task AuditAsync(Guid tenantId, IReadOnlyCollection<RemovedLink> links, CancellationToken ct)
    {
        var (actorKind, actorId) = AuditActorResolver.Resolve(currentUser);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        foreach (var link in links)
        {
            var changes = JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(RemovedLink.Id)] = Old(link.Id),
                [nameof(RemovedLink.AccountId)] = Old(link.AccountId),
                [nameof(RemovedLink.BusinessTenantId)] = Old(link.BusinessTenantId),
                [nameof(RemovedLink.ChatId)] = Old(link.ChatId),
                [nameof(RemovedLink.ConsumerTenantId)] = Old(link.ConsumerTenantId),
                [nameof(RemovedLink.DisplayNumber)] = Old(link.DisplayNumber),
                [nameof(RemovedLink.LinkedAtUtc)] = Old(link.LinkedAtUtc),
                [nameof(RemovedLink.OrganizationName)] = Old(link.OrganizationName),
            });
            var entry = AuditEntry.Create(tenantId, actorKind, actorId, AuditAction.Deleted, nameof(WhatsAppConversationLink), link.Id,
                changes, nowUtc);
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO tenant."AuditEntries" ("TenantId", "Id", "ActorKind", "ActorId", "Action", "EntityType", "EntityId", "Changes",
                    "OccurredAtUtc")
                VALUES ({entry.TenantId}, {entry.Id}, {entry.ActorKind.ToString()}, {entry.ActorId}, {entry.Action.ToString()},
                    {entry.EntityType}, {entry.EntityId}, CAST({entry.Changes} AS jsonb), {entry.OccurredAtUtc})
                """, ct);
        }

        static Dictionary<string, object?> Old(object value) => new(StringComparer.Ordinal) { ["old"] = value, ["new"] = null };
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

    private sealed class RemovedLink
    {
        public Guid Id { get; init; }
        public Guid ConsumerTenantId { get; init; }
        public Guid BusinessTenantId { get; init; }
        public Guid ChatId { get; init; }
        public Guid AccountId { get; init; }
        public string OrganizationName { get; init; } = string.Empty;
        public string DisplayNumber { get; init; } = string.Empty;
        public DateTime LinkedAtUtc { get; init; }
    }
#pragma warning restore CA1812
}
