using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>El bot del número de Plataforma, con la opción del sistema «Entrar a mi cuenta».</summary>
public interface IPlatformWhatsAppBotService
{
    Task<Result<PlatformWhatsAppBotResponse>> GetAsync(CancellationToken cancellationToken);
    Task<Result<PlatformWhatsAppBotResponse>> UpdateAsync(UpdatePlatformWhatsAppBotRequest request, CancellationToken cancellationToken);
}
