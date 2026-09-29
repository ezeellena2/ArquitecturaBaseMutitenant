namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Technical sender for one registered outbox channel.</summary>
public interface IChannelSender
{
    string Key { get; }

    Task SendAsync(string payload, CancellationToken cancellationToken);
}
