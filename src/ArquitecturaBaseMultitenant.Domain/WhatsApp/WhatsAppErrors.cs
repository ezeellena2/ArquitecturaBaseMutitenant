using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;
public static class WhatsAppErrors
{
    public const string CountryNotAllowedCode = "WhatsApp.Phone.CountryNotAllowed";
    public const string WebhookTooManyRequestsCode = "WhatsApp.Webhook.TooManyRequests";
    public static readonly Error CountryNotAllowed = Error.Validation(CountryNotAllowedCode, "WhatsApp is not enabled for that country.");
    public static Error WebhookTooManyRequests(int retryAfter) => Error.TooManyRequests(WebhookTooManyRequestsCode,
        "Too many webhook requests.", new Dictionary<string, object?> { ["retryAfter"] = retryAfter });
    public static readonly Error InvalidSignature = Error.Unauthorized("WhatsApp.Webhook.InvalidSignature", "Invalid webhook signature.");
    public static readonly Error InvalidPayload = Error.Validation("WhatsApp.Webhook.InvalidPayload", "Invalid webhook payload.");
    public static readonly Error LeaseExpired = Error.Conflict("WhatsApp.Inbound.LeaseExpired", "Inbound message lease expired.");
}
