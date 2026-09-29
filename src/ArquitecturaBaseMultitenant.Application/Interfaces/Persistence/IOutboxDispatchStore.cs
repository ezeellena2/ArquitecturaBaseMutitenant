using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Locks due messages and tracks delivery state inside the caller's UnitOfWork.</summary>
public interface IOutboxDispatchStore
{
    Task<int> DispatchDueAsync(IReadOnlyCollection<IChannelSender> senders,
        CancellationToken cancellationToken);
}
