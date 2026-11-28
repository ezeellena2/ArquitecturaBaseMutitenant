using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>El envío de las difusiones de la organización actual, por tandas, desde el worker.</summary>
public interface IWhatsAppBroadcastDispatchService
{
    /// <summary>Manda una tanda de la difusión que más espera; true si hizo algo (puede quedar más).</summary>
    Task<Result<bool>> DispatchNextAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Con el módulo apagado, saca de la cola una tanda de lo que una difusión todavía no mandó y devuelve esos destinatarios a
    /// pendiente: la difusión queda en pausa y los retoma al prenderlo. True si sacó una tanda entera (puede quedar más).
    /// </summary>
    Task<Result<bool>> PauseNextAsync(CancellationToken cancellationToken);
}
