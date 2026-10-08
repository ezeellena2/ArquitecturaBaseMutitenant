using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Disabled;
internal sealed class DisabledWhatsAppAvailability : IWhatsAppAvailability
{
    public bool IsEnabled => false;
    public bool IsWebhookEnabled => false;
}
