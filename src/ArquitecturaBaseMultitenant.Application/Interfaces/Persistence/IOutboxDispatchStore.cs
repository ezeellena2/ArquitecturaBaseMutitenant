using ArquitecturaBaseMultitenant.Application.Models.Messaging;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Reserva, devuelve y confirma filas dentro de la UoW del llamador; no recibe transporte ni descifra.</summary>
public interface IOutboxDispatchStore
{
    Task<OutboxDispatchClaim?> ClaimNextAsync(IReadOnlyCollection<string> channels, CancellationToken cancellationToken);
    /// <returns>El canal si se aplicó el resultado al lease actual; null si ya no corresponde.</returns>
    Task<string?> CompleteAsync(Guid messageId, Guid leaseId, OutboxDeliveryReceipt receipt, CancellationToken cancellationToken);
    /// <summary>Devuelve a pendiente una reserva que no llegó al transporte, sin gastar el intento.</summary>
    Task<bool> ReleaseAsync(Guid messageId, Guid leaseId, CancellationToken cancellationToken);
}
