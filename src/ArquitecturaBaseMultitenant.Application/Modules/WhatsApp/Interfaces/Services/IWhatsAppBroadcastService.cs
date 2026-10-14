using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>Difusiones a los contactos que aceptan promociones, con costo estimado y resultados por contacto.</summary>
public interface IWhatsAppBroadcastService
{
    Task<Result<WhatsAppBroadcastListResponse>> ListAsync(WhatsAppBroadcastListRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<WhatsAppTemplateOptionResponse>>> ListTemplateOptionsAsync(WhatsAppTemplateOptionListRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<string>>> ListTagsAsync(WhatsAppTagListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppBroadcastDetailResponse>> GetAsync(Guid id, WhatsAppBroadcastDetailRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppBroadcastEstimateResponse>> EstimateAsync(WhatsAppBroadcastEstimateRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppBroadcastResponse>> CreateAsync(CreateWhatsAppBroadcastRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppBroadcastResponse>> CancelAsync(Guid id, CancelWhatsAppBroadcastRequest request, CancellationToken cancellationToken);
}
