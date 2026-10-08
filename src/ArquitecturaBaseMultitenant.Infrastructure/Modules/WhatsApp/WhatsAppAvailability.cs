using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
internal sealed record WhatsAppAvailability(bool IsEnabled, bool IsWebhookEnabled) : IWhatsAppAvailability;
