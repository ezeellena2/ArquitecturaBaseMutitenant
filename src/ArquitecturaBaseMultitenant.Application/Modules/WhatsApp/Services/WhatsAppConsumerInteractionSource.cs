using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Engagement;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Engagement;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Las conversaciones vinculadas como interacciones de la persona con organizaciones (fuente <c>whatsapp</c>): la misma lectura
/// que «Mensajes», solo desde su espacio personal.
/// </summary>
internal sealed class WhatsAppConsumerInteractionSource(IWhatsAppConversationLinkReader reader, ITenantContext tenant) : IConsumerInteractionSource
{
    public string Key => WhatsAppFeature.Key;

    public async Task<Result<PagedResult<ConsumerInteractionResponse>>> ListAsync(ListConsumerInteractionsRequest request, CancellationToken cancellationToken)
    {
        if (tenant.TenantKind != TenantKind.Personal) return new PagedResult<ConsumerInteractionResponse>([], request.Page, request.PageSize, 0);
        var page = await reader.ListAsync(request, cancellationToken);
        return new PagedResult<ConsumerInteractionResponse>([.. page.Items.Select(conversation => new ConsumerInteractionResponse(conversation.Id,
            Key, conversation.OrganizationId, conversation.OrganizationName, conversation.LastMessagePreview ?? string.Empty,
            conversation.LastMessageAtUtc ?? conversation.LinkedAtUtc))], page.Page, page.PageSize, page.TotalCount);
    }
}
