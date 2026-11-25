using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// Reclama y confirma un mensaje en transacciones distintas; el transporte ocurre entre ambos pasos. Lo que no llegó al
/// transporte se devuelve a la cola.
/// </summary>
public interface IOutboxDispatchService
{
    Task<Result<OutboxDispatchClaim?>> ClaimNextAsync(IReadOnlyCollection<string> channels, CancellationToken cancellationToken);
    Task<Result> CompleteAsync(Guid messageId, Guid leaseId, OutboxDeliveryReceipt receipt, CancellationToken cancellationToken);
    Task<Result> ReleaseAsync(Guid messageId, Guid leaseId, CancellationToken cancellationToken);
}
