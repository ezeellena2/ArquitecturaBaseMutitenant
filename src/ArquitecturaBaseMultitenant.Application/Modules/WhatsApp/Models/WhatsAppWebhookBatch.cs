using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
public sealed record WhatsAppWebhookBatch(IReadOnlyList<WhatsAppWebhookMessage> Messages, IReadOnlyList<WhatsAppWebhookStatus> Statuses)
{
    public override string ToString() => nameof(WhatsAppWebhookBatch);
}
public sealed record WhatsAppWebhookMessage(string PhoneNumberId, string Wamid, string Sender, WhatsAppMessageKind Kind,
    string? Text, string? ReplyId, DateTime OccurredAtUtc, string? ContentType = null)
{
    public override string ToString() => nameof(WhatsAppWebhookMessage);
}
/// <summary>Un estado de entrega con el precio que informó Meta (decisión 8) y, si falló, su código.</summary>
public sealed record WhatsAppWebhookStatus(string PhoneNumberId, string Wamid, WhatsAppMessageStatus Status, DateTime OccurredAtUtc,
    string? PricingCategory = null, string? PricingType = null, int? ErrorCode = null)
{
    public override string ToString() => nameof(WhatsAppWebhookStatus);
}
