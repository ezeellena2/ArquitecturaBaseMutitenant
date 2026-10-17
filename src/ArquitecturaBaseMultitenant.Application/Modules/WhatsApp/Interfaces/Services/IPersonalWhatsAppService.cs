using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>«Mensajes» de la persona: sus conversaciones vinculadas, de solo lectura, desde que las vinculó.</summary>
public interface IPersonalWhatsAppService
{
    Task<Result<PagedResult<PersonalWhatsAppConversationResponse>>> ListAsync(PersonalWhatsAppConversationListRequest request, CancellationToken cancellationToken);
    Task<Result<PersonalWhatsAppConversationDetailResponse>> GetAsync(Guid id, WhatsAppConversationMessagesRequest request, CancellationToken cancellationToken);
    Task<Result> UnlinkAsync(Guid id, CancellationToken cancellationToken);
}
