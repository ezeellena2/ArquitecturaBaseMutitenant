using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>El número de Plataforma: alta, consultas y bot con la opción del sistema.</summary>
public interface IPlatformWhatsAppNumberService
{
    Task<Result<PlatformWhatsAppNumberResponse>> GetAsync(CancellationToken cancellationToken);
    Task<Result<PlatformWhatsAppNumberResponse>> ConnectAsync(ConnectPlatformWhatsAppNumberRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformWhatsAppNumberResponse>> CheckAsync(CheckWhatsAppNumberRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformWhatsAppNumberResponse>> UpdateAsync(UpdatePlatformWhatsAppNumberRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformWhatsAppBotResponse>> GetBotAsync(CancellationToken cancellationToken);
    Task<Result<PlatformWhatsAppBotResponse>> UpdateBotAsync(UpdatePlatformWhatsAppBotRequest request, CancellationToken cancellationToken);
}
