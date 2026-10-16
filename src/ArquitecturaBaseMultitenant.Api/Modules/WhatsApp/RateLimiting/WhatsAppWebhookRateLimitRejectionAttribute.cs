using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.RateLimiting;

/// <summary>El webhook conserva su propio código al llegar a su cuota (E9); la cuota la cuenta el núcleo.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class WhatsAppWebhookRateLimitRejectionAttribute : Attribute, IRateLimitRejection
{
    public Error Rejected(int retryAfterSeconds) => WhatsAppErrors.WebhookTooManyRequests(retryAfterSeconds);
}
