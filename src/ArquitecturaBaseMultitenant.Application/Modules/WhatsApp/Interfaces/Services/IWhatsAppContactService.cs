using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>Contactos de la organización, sus etiquetas, el consentimiento de promociones y la importación.</summary>
public interface IWhatsAppContactService
{
    Task<Result<WhatsAppContactListResponse>> ListAsync(WhatsAppContactListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppContactResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<PagedResult<WhatsAppContactConversationResponse>>> ListConversationsAsync(Guid id, WhatsAppContactChildListRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<WhatsAppContactBroadcastResponse>>> ListBroadcastsAsync(Guid id, WhatsAppContactChildListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppContactResponse>> UpdateAsync(Guid id, UpdateWhatsAppContactRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<string>>> ListTagsAsync(WhatsAppTagListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppFileResponse>> GetImportSampleAsync(CancellationToken cancellationToken);
    Task<Result<WhatsAppImportPreviewResponse>> PreviewImportAsync(ImportWhatsAppContactsRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppImportResponse>> ImportAsync(ImportWhatsAppContactsRequest request, CancellationToken cancellationToken);
}
