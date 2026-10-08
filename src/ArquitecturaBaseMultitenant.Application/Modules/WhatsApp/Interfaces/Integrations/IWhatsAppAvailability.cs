namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
public interface IWhatsAppAvailability
{
    bool IsEnabled { get; }
    bool IsWebhookEnabled { get; }
}
