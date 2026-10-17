using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>El bot de cada número de la organización: textos, horario, menú y «Además».</summary>
public interface IWhatsAppBotService
{
    Task<Result<WhatsAppBotResponse>> GetAsync(Guid numberId, CancellationToken cancellationToken);
    Task<Result<WhatsAppBotResponse>> UpdateAsync(Guid numberId, UpdateWhatsAppBotRequest request, CancellationToken cancellationToken);
}
