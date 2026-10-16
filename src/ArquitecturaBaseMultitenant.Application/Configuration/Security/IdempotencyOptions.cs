using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Security;

/// <summary>
/// Cuánto vale una clave idempotente y cada cuánto se retiran las vencidas (E9), en Idempotency: 24 horas y una vez por
/// hora, como siempre.
/// </summary>
public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    [Range(1, 168)]
    public int ValidityHours { get; init; } = 24;

    [Range(1, 1440)]
    public int CleanupIntervalMinutes { get; init; } = 60;
}
