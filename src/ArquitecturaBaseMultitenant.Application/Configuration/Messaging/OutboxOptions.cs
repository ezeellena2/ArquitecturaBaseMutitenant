using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Messaging;

/// <summary>
/// Ritmo de lectura y reintentos del outbox persistente. Cada canal puede tener su propia espera de entrega y su tope de
/// intentos (<see cref="Channels"/>); sin ellos vale lo general. La entrega y la confirmación caben en el apagado del host:
/// lo comprueba <c>OutboxOptionsValidator</c> al arrancar.
/// </summary>
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

    // Mayor que la entrega más la confirmación: una reserva viva nunca se toma por interrumpida.
    [Range(180, 3600)]
    public int LeaseSeconds { get; init; } = 300;

    // La entrega y la confirmación juntas caben en los 30 s que el host da para apagarse.
    [Range(5, 60)]
    public int CompletionTimeoutSeconds { get; init; } = 10;

    [Range(5, 60)]
    public int DeliveryTimeoutSeconds { get; init; } = 15;

    /// <summary>
    /// Por clave de canal (<c>email</c>, <c>whatsapp</c>): la espera de la entrega y el tope de intentos. Sin distinguir
    /// mayúsculas, como toda clave de configuración: <c>Channels:Email</c> vale para <c>email</c>.
    /// </summary>
    public Dictionary<string, OutboxChannelOptions> Channels { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public int MaxAttemptsFor(string channel) =>
        Channels.TryGetValue(channel, out var own) && own.MaxAttempts is { } attempts ? attempts : MaxAttempts;

    public TimeSpan DeliveryTimeoutFor(string channel) => TimeSpan.FromSeconds(
        Channels.TryGetValue(channel, out var own) && own.DeliveryTimeoutSeconds is { } seconds ? seconds : DeliveryTimeoutSeconds);
}
