using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>Números de la organización: alta embebida, medio de pago, a quién atienden y su bot.</summary>
public interface IWhatsAppNumberService
{
    Task<Result<WhatsAppNumberListResponse>> ListAsync(WhatsAppNumberListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppNumberResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<PagedResult<WhatsAppReferenceResponse>>> ListCompanyOptionsAsync(WhatsAppCompanyOptionListRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppNumberResponse>> ConnectAsync(ConnectWhatsAppNumberRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppNumberResponse>> CheckAsync(Guid id, CheckWhatsAppNumberRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppNumberResponse>> UpdateAsync(Guid id, UpdateWhatsAppNumberRequest request, CancellationToken cancellationToken);
    Task<Result> DisconnectAsync(Guid id, DeleteWhatsAppNumberRequest request, CancellationToken cancellationToken);
    Task<Result<WhatsAppBotResponse>> GetBotAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<WhatsAppBotResponse>> UpdateBotAsync(Guid id, UpdateWhatsAppBotRequest request, CancellationToken cancellationToken);
}
