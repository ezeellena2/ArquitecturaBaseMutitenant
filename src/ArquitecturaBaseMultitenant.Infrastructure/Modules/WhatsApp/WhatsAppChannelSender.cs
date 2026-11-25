using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

internal sealed class WhatsAppChannelSender(WhatsAppCloudClient client) : IChannelReceiptSender
{
    public string Key => "whatsapp";

    public async Task<OutboxDeliveryReceipt> SendWithReceiptAsync(string payload, CancellationToken cancellationToken)
    {
        WhatsAppOutboundMessage? message;
        try { message = JsonSerializer.Deserialize<WhatsAppOutboundMessage>(payload); }
        catch (JsonException) { message = null; }
        // Un payload que no se puede leer no sale nunca: falla sin llamar a Meta, como el del correo. Si saliera como excepción,
        // la cola lo tomaría como «Sin confirmar».
        if (message is null) return OutboxDeliveryReceipt.Rejected(WhatsAppSendFailures.Rejected, retryable: false);
        return await client.SendAsync(message, cancellationToken);
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        var receipt = await SendWithReceiptAsync(payload, cancellationToken);
        if (!receipt.IsAccepted) throw new InvalidOperationException(receipt.SafeCode);
    }
}
