using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Messaging;

/// <summary>Ritmo de lectura y reintentos del outbox persistente.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Messaging:Outbox";

    [Range(1, 100)]
    public int BatchSize { get; init; } = 25;

    [Range(1, 60)]
    public int PollIntervalSeconds { get; init; } = 10;

    [Range(1, 20)]
    public int MaxAttempts { get; init; } = 5;

    [Range(1, 3600)]
    public int RetryBaseDelaySeconds { get; init; } = 30;

    // El timeout HTTP del proveedor es 60 s; quedan márgenes para el envío y el commit final.
    [Range(180, 3600)]
    public int LeaseSeconds { get; init; } = 300;

    [Range(5, 60)]
    public int CompletionTimeoutSeconds { get; init; } = 30;
}
