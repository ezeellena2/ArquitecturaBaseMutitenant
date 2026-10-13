using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>Bandejas de atención: Clientes o Internos de la organización (inbox) o las consultas de Plataforma (inbox null), según el acceso.</summary>
public interface IWhatsAppConversationService
{
    Task<Result<WhatsAppConversationListResponse>> ListAsync(WhatsAppInbox? inbox, WhatsAppConversationListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppConversationResponse>> GetAsync(WhatsAppInbox? inbox, Guid id, WhatsAppConversationMessagesRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppConversationRowResponse>> TakeAsync(WhatsAppInbox? inbox, Guid id, CancellationToken cancellationToken);
    Task<Result<WhatsAppChatMessageResponse>> SendAsync(WhatsAppInbox? inbox, Guid id, SendWhatsAppMessageRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppChatMessageResponse>> AddNoteAsync(WhatsAppInbox? inbox, Guid id, AddWhatsAppNoteRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppConversationRowResponse>> TransferAsync(WhatsAppInbox? inbox, Guid id, TransferWhatsAppConversationRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<WhatsAppTemplateOptionResponse>>> ListTemplateOptionsAsync(WhatsAppInbox? inbox, Guid id, WhatsAppTemplateOptionListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppChatMessageResponse>> SendTemplateAsync(WhatsAppInbox? inbox, Guid id, SendWhatsAppTemplateRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<WhatsAppTransferTeamResponse>>> ListTransferTeamsAsync(WhatsAppInbox? inbox, Guid id, WhatsAppTransferOptionListRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<WhatsAppTransferPersonResponse>>> ListTransferPeopleAsync(WhatsAppInbox? inbox, Guid id, WhatsAppTransferOptionListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppUntakenResponse>> GetUntakenAsync(CancellationToken cancellationToken);
}
