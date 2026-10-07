using ArquitecturaBaseMultitenant.Application.Models.Legal;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lista sólo metadatos vencidos pendientes, sin abrir alcances ni leer copias personales.</summary>
public interface IConsumerDataRetentionReader
{
    Task<IReadOnlyList<ConsumerDataRetentionWork>> ListExpiredAsync(DateTime nowUtc, int maximumCount,
        CancellationToken cancellationToken);
}
