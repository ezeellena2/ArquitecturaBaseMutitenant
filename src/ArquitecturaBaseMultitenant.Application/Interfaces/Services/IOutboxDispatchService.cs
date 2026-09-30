using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Despacha una tanda de mensajes pendientes por sus canales y confirma su estado dentro de la transacción.</summary>
public interface IOutboxDispatchService
{
    Task<Result<int>> DispatchOnceAsync(IReadOnlyCollection<IChannelSender> senders,
        CancellationToken cancellationToken);
}
