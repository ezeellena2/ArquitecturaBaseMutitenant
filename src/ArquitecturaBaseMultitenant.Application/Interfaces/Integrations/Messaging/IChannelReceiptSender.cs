using ArquitecturaBaseMultitenant.Application.Models.Messaging;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
/// <summary>Extensión opcional del transporte para conservar una aceptación o un código de falla seguro.</summary>
public interface IChannelReceiptSender : IChannelSender
{
    Task<OutboxDeliveryReceipt> SendWithReceiptAsync(string payload, CancellationToken cancellationToken);
}
