using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>El envío de las difusiones de la organización actual, por tandas, desde el worker.</summary>
public interface IWhatsAppBroadcastDispatchService
{
    /// <summary>Manda una tanda de la difusión que más espera; true si hizo algo (puede quedar más).</summary>
    Task<Result<bool>> DispatchNextAsync(CancellationToken cancellationToken);
}
