using ArquitecturaBaseMultitenant.Application.Models.Home;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;

/// <summary>
/// La tarjeta de un módulo en el Inicio de la organización (por ejemplo «WhatsApp · 2 números»). Lee con el tenant del
/// claim, revisa su módulo y los permisos del contexto, y devuelve null si quien mira no la ve.
/// </summary>
public interface IModuleHomeCardSource
{
    Task<HomeModuleCard?> GetAsync(PendingItemContext context, CancellationToken cancellationToken);
}
