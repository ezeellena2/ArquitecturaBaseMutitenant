using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IOutboxDispatchService
{
    Task<Result<int>> DispatchOnceAsync(IReadOnlyCollection<IChannelSender> senders,
        CancellationToken cancellationToken);
}
