using ArquitecturaBaseMultitenant.Application.Models.Messaging;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
/// <summary>Registra metadatos del proveedor en la UoW de confirmación, sin enviar ni abrir otro límite.</summary>
public interface IOutboxDeliveryObserver
{
    string Key { get; }
    Task RecordAcceptedAsync(Guid messageId, OutboxDeliveryReceipt receipt, DateTime acceptedAtUtc,
        CancellationToken cancellationToken);
}
