using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Messaging;

/// <summary>Lo propio de un canal del outbox (<c>Messaging:Outbox:Channels:&lt;canal&gt;</c>); lo que falta toma lo general.</summary>
public sealed class OutboxChannelOptions
{
    [Range(5, 60)]
    public int? DeliveryTimeoutSeconds { get; init; }

    [Range(1, 20)]
    public int? MaxAttempts { get; init; }
}
