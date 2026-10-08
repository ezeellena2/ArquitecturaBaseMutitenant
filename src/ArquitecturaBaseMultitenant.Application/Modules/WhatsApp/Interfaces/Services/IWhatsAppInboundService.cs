using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
public interface IWhatsAppInboundService
{
    Task<Result<WhatsAppInboundClaim?>> ClaimAsync(CancellationToken ct);
    Task<Result> ProcessAsync(WhatsAppInboundClaim claim, CancellationToken ct);
    Task<Result> FailAsync(WhatsAppInboundClaim claim, CancellationToken ct);
}
