using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>«Mensajes» de la persona: el filtro de partes deja ver solo sus vínculos.</summary>
internal sealed class WhatsAppConversationLinkReader(ApplicationDbContext context) : IWhatsAppConversationLinkReader
{
    public Task<PagedResult<WhatsAppConversationLinkRow>> ListAsync(PagedRequest request, CancellationToken ct) =>
        Rows(context.Set<WhatsAppConversationLink>().AsNoTracking()
                .OrderByDescending(link => link.LastMessageAtUtc ?? link.LinkedAtUtc).ThenByDescending(link => link.Id))
            .ToPagedResultAsync(request, ct);

    public Task<WhatsAppConversationLinkRow?> FindAsync(Guid id, CancellationToken ct) =>
        Rows(context.Set<WhatsAppConversationLink>().AsNoTracking().Where(link => link.Id == id)).SingleOrDefaultAsync(ct);

    public Task<PagedResult<WhatsAppChatMessageResponse>> ListMessagesAsync(Guid linkId, PagedRequest request, CancellationToken ct) =>
        context.Set<WhatsAppLinkedMessage>().AsNoTracking().Where(message => message.LinkId == linkId)
            .OrderByDescending(message => message.CreatedAtUtc).ThenByDescending(message => message.Id)
            .Select(message => new WhatsAppChatMessageResponse(message.Id,
                message.MediaKind == null ? WhatsAppChatMessageKind.Text : WhatsAppChatMessageKind.Media, message.Direction, null, message.Text,
                message.MediaKind, message.Direction == WhatsAppMessageDirection.Inbound ? WhatsAppChatMessageStatus.Received : WhatsAppChatMessageStatus.Sent,
                null, message.CreatedAtUtc))
            .ToPagedResultAsync(request, ct);

    private static IQueryable<WhatsAppConversationLinkRow> Rows(IQueryable<WhatsAppConversationLink> links) =>
        links.Select(link => new WhatsAppConversationLinkRow(link.Id, link.BusinessTenantId, link.OrganizationName, link.LinkedAtUtc,
            link.LastMessageAtUtc, link.LastMessagePreview, link.DisplayNumber));
}
