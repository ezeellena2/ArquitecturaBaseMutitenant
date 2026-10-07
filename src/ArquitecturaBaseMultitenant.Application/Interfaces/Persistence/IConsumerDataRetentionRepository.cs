using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Bloquea y relee una retención vencida dentro de la transacción del servicio.</summary>
public interface IConsumerDataRetentionRepository
{
    Task<ConsumerDataRetention?> GetForProcessingAsync(ConsumerDataRetentionWork work, DateTime nowUtc,
        CancellationToken cancellationToken);
}
