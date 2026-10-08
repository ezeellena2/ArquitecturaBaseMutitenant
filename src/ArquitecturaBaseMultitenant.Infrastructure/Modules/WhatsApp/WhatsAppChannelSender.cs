using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
internal sealed class WhatsAppChannelSender(WhatsAppCloudClient client) : IChannelReceiptSender
{
    public string Key => "whatsapp";
    public Task<OutboxDeliveryReceipt> SendWithReceiptAsync(string payload, CancellationToken cancellationToken) =>
        client.SendAsync(JsonSerializer.Deserialize<WhatsAppOutboundMessage>(payload)
            ?? throw new InvalidOperationException("Invalid encrypted WhatsApp payload."), cancellationToken);
    public async Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        var receipt = await SendWithReceiptAsync(payload, cancellationToken);
        if (!receipt.IsAccepted) throw new InvalidOperationException(receipt.SafeCode);
    }
}
