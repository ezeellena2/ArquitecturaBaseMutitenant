using System.Linq.Expressions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>
/// Los lotes de la purga de los chats (272). Cada consulta nombra la organización del alcance además de la RLS, y pregunta solo
/// si el contenido es nulo: nunca lo compara ni lo ordena (ADR 0065). Una fila tomada por otra transacción queda para la próxima.
/// Cada fila vuelve con su contenido marcado para escribirse: lo que el anillo no abre se lee nulo, y vaciarlo no sería un cambio
/// para EF; sin la marca, el cifrado quedaría en la base y el lote siguiente volvería a tomar la misma fila, para siempre.
/// </summary>
internal sealed class WhatsAppChatRetentionRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppChatRetentionRepository
{
    public async Task<IReadOnlyList<WhatsAppChatMessage>> ListExpiredMessagesAsync(DateTime beforeUtc, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return Written(await context.Set<WhatsAppChatMessage>().FromSqlInterpolated($"""
            SELECT * FROM tenant."WhatsAppChatMessages"
            WHERE "TenantId" = {tenantId} AND ("Text" IS NOT NULL OR "AuthorName" IS NOT NULL) AND "CreatedAtUtc" <= {beforeUtc}
            ORDER BY "CreatedAtUtc", "Id" LIMIT {limit} FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct), message => message.Text);
    }

    public async Task<IReadOnlyList<WhatsAppChat>> ListExpiredPreviewsAsync(DateTime beforeUtc, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return Written(await context.Set<WhatsAppChat>().FromSqlInterpolated($"""
            SELECT *, xmin FROM tenant."WhatsAppChats"
            WHERE "TenantId" = {tenantId} AND "LastMessagePreview" IS NOT NULL AND "LastMessageAtUtc" <= {beforeUtc}
            ORDER BY "LastMessageAtUtc", "Id" LIMIT {limit} FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct), chat => chat.LastMessagePreview);
    }

    public async Task<IReadOnlyList<WhatsAppConversationLink>> ListExpiredLinkPreviewsAsync(DateTime beforeUtc, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return Written(await context.Set<WhatsAppConversationLink>().FromSqlInterpolated($"""
            SELECT * FROM engagement."WhatsAppConversationLinks"
            WHERE "BusinessTenantId" = {tenantId} AND "LastMessagePreview" IS NOT NULL AND "LastMessageAtUtc" <= {beforeUtc}
            ORDER BY "LastMessageAtUtc", "Id" LIMIT {limit} FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct), link => link.LastMessagePreview);
    }

    public async Task<IReadOnlyList<WhatsAppLinkedMessage>> ListExpiredLinkedMessagesAsync(DateTime beforeUtc, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return Written(await context.Set<WhatsAppLinkedMessage>().FromSqlInterpolated($"""
            SELECT * FROM engagement."WhatsAppLinkedMessages"
            WHERE "BusinessTenantId" = {tenantId} AND "Text" IS NOT NULL AND "CreatedAtUtc" <= {beforeUtc}
            ORDER BY "CreatedAtUtc", "Id" LIMIT {limit} FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct), message => message.Text);
    }

    public async Task<IReadOnlyList<PlatformWhatsAppChatMessage>> ListExpiredPlatformMessagesAsync(DateTime beforeUtc, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        return Written(await context.Set<PlatformWhatsAppChatMessage>().FromSqlInterpolated($"""
            SELECT * FROM platform."PlatformWhatsAppChatMessages"
            WHERE ("Text" IS NOT NULL OR "AuthorName" IS NOT NULL) AND "CreatedAtUtc" <= {beforeUtc}
            ORDER BY "CreatedAtUtc", "Id" LIMIT {limit} FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct), message => message.Text);
    }

    public async Task<IReadOnlyList<PlatformWhatsAppChat>> ListExpiredPlatformPreviewsAsync(DateTime beforeUtc, int limit, CancellationToken ct)
    {
        context.RequireTransaction();
        return Written(await context.Set<PlatformWhatsAppChat>().FromSqlInterpolated($"""
            SELECT *, xmin FROM platform."PlatformWhatsAppChats"
            WHERE "LastMessagePreview" IS NOT NULL AND "LastMessageAtUtc" <= {beforeUtc}
            ORDER BY "LastMessageAtUtc", "Id" LIMIT {limit} FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(ct), chat => chat.LastMessagePreview);
    }

    private T[] Written<T>(T[] rows, Expression<Func<T, string?>> content) where T : class
    {
        foreach (var row in rows) context.Entry(row).Property(content).IsModified = true;
        return rows;
    }
}
