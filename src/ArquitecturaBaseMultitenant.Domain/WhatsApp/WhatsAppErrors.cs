using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;
public static class WhatsAppErrors
{
    public static readonly Error InvalidSignature = Error.Unauthorized("WhatsApp.Webhook.InvalidSignature", "Invalid webhook signature.");
    public static readonly Error InvalidPayload = Error.Validation("WhatsApp.Webhook.InvalidPayload", "Invalid webhook payload.");
    public static readonly Error LeaseExpired = Error.Conflict("WhatsApp.Inbound.LeaseExpired", "Inbound message lease expired.");
}
