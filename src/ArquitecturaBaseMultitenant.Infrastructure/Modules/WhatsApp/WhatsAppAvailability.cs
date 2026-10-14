using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
/// <summary>
/// El número de Plataforma atiende si hay uno conectado; el webhook es de la instalación (los números de las
/// organizaciones también lo usan) y necesita los dos secretos.
/// </summary>
internal sealed class WhatsAppAvailability(WhatsAppPlatformChannelState state, IOptions<WhatsAppOptions> options) : IWhatsAppAvailability
{
    public bool IsEnabled => state.Current is not null;
    public bool IsWebhookEnabled => options.Value.HasWebhookSecrets;
}
