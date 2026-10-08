using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
public sealed record WhatsAppWebhookBatch(IReadOnlyList<WhatsAppWebhookMessage> Messages, IReadOnlyList<WhatsAppWebhookStatus> Statuses)
{
    public override string ToString() => nameof(WhatsAppWebhookBatch);
}
public sealed record WhatsAppWebhookMessage(string PhoneNumberId, string Wamid, string Sender, WhatsAppMessageKind Kind,
    string? Text, string? ReplyId, DateTime OccurredAtUtc)
{
    public override string ToString() => nameof(WhatsAppWebhookMessage);
}
public sealed record WhatsAppWebhookStatus(string PhoneNumberId, string Wamid, WhatsAppMessageStatus Status, DateTime OccurredAtUtc)
{
    public override string ToString() => nameof(WhatsAppWebhookStatus);
}
