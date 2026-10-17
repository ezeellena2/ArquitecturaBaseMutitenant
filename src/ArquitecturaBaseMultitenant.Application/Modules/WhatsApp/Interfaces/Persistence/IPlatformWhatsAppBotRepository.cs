using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>El bot del número de Plataforma (uno solo). Exige la transacción del caso de uso.</summary>
public interface IPlatformWhatsAppBotRepository
{
    /// <summary>Lo lee bajo su lock; con versión, la exige. Sin fila, responde con los valores iniciales.</summary>
    Task<PlatformWhatsAppBot?> GetAsync(uint? expectedVersion, CancellationToken ct);
    void Add(PlatformWhatsAppBot bot);
}
