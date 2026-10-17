using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>El número de Plataforma: alta y consultas. Su bot, en IPlatformWhatsAppBotService.</summary>
public interface IPlatformWhatsAppNumberService
{
    Task<Result<PlatformWhatsAppNumberResponse>> GetAsync(CancellationToken cancellationToken);
    Task<Result<PlatformWhatsAppNumberResponse>> ConnectAsync(ConnectPlatformWhatsAppNumberRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformWhatsAppNumberResponse>> CheckAsync(CheckWhatsAppNumberRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformWhatsAppNumberResponse>> UpdateAsync(UpdatePlatformWhatsAppNumberRequest request, CancellationToken cancellationToken);
}
