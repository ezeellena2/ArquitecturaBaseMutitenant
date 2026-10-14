using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Los bots de los números de la organización del claim. Exige la transacción del caso de uso.</summary>
public interface IWhatsAppBotRepository
{
    /// <summary>Serializa la configuración del bot de un número, aunque todavía no exista.</summary>
    Task LockAsync(Guid channelId, CancellationToken ct);
    /// <summary>El bot del número; con versión, la exige. Sin fila, el número responde con los valores iniciales.</summary>
    Task<WhatsAppBot?> GetAsync(Guid channelId, uint? expectedVersion, CancellationToken ct);
    void Add(WhatsAppBot bot);
}
