using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppDeliveryObserver(IWhatsAppMessageRepository messages) : IOutboxDeliveryObserver
{
    public string Key => "whatsapp";
    public async Task RecordAcceptedAsync(Guid messageId, OutboxDeliveryReceipt receipt, DateTime acceptedAtUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(receipt.ProviderMessageId) || string.IsNullOrWhiteSpace(receipt.ProviderScope)) return;
        await messages.LockStreamAsync(receipt.ProviderScope, receipt.ProviderMessageId, cancellationToken);
        if (await messages.GetEventAsync(receipt.ProviderScope, receipt.ProviderMessageId, "accepted", cancellationToken) is not null) return;
        var kind = receipt.Category == nameof(WhatsAppMessageKind.Authentication) ? WhatsAppMessageKind.Authentication : WhatsAppMessageKind.Template;
        var message = WhatsAppMessage.Create(receipt.ProviderScope, receipt.ProviderMessageId, "accepted", WhatsAppMessageDirection.Outbound,
            kind, WhatsAppMessageStatus.Sent, acceptedAtUtc, acceptedAtUtc, outboxMessageId: messageId);
        message.MarkOutboundAccepted(acceptedAtUtc);
        messages.Add(message);
    }
}
