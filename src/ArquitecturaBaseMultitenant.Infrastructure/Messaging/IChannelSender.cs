namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>Emisor técnico de un canal; el dispatcher lo llama después del commit.</summary>
internal interface IChannelSender
{
    string Key { get; }

    Task SendAsync(string payload, CancellationToken cancellationToken);
}
